using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// P1 controls the TOP half of the shared body: aiming, leaning, the AK-47.
/// Ownership: only the client whose clientId matches TopHalfClient runs input;
/// hits/spawns are decided on the server; visuals are client-local.
/// Mouse: locked to the game while playing; ESC → PauseController frees it for
/// the pause/settings menus; the game-over panel frees it too.
/// </summary>
public class PlayerOneController : NetworkBehaviour
{
    [Header("Lean (Q/E — Rainbow-style)")]
    [Tooltip("Top-half mesh. Rolls on a HINGE at its bottom so the head swings out while the base stays planted.")]
    [SerializeField] private Transform torso;
    [Tooltip("The camera's seat. Slides with the head's lean arc; also carries the view roll + recoil kick.")]
    [SerializeField] private Transform camAnchor;
    [Tooltip("Max lean in degrees. +Q leans screen-left, -E leans screen-right.")]
    [SerializeField] private float leanAngle  = 15f;
    [Tooltip("Smoothing rate. 14 ≈ snappy (~70ms settle), 18-20 ≈ near-instant.")]
    [SerializeField] private float leanSnap   = 14f;
    [Tooltip("Torso-LOCAL Y of the hinge point (bottom of the capsule, where it meets P2's legs). Negative = below center.")]
    [SerializeField] private float leanHingeY = -0.5f;
    [Tooltip("Torso-LOCAL Y of the head — where the camera rides from. Set so the camera sits at the capsule top when upright.")]
    [SerializeField] private float leanHeadY  = 0.35f;
    [Tooltip("1 = camera rides the head exactly; 0.6-0.8 = subtler peek; 0 = camera never moves.")]
    [SerializeField] private float camFollow  = 1f;

    [Header("Aim")]
    [Tooltip("P1's camera. The CENTER of this camera is the aim truth — every ray starts here.")]
    [SerializeField] private Camera topCam;
    [Tooltip("LookPivot. Used for the shooter's collider root lookup and the fire ray fallback.")]
    [SerializeField] private Transform aimOrigin;
    [SerializeField] private float hipFov = 60f, aimFov = 35f, aimSpeed = 10f;

    [Header("AK-47")]
    [Tooltip("Full-auto rate. 600 RPM = one shot every 0.1s while the trigger is held.")]
    [SerializeField] private float roundsPerMinute = 600f;
    [Tooltip("Degrees the muzzle (and the bullets' aim) rise per shot. 0.6 ≈ gentle climb, 1.0+ ≈ wild spray.")]
    [SerializeField] private float kickPerShot     = 0.6f;
    [Tooltip("How fast the muzzle settles back down after a burst. Lower = longer climb.")]
    [SerializeField] private float recoilRecover   = 6f;
    [Tooltip("Rounds per mag. ⚠ The Inspector value on the prefab overrides this default — set 60 there too.")]
    [SerializeField] private int   magSize         = 60;
    [Tooltip("Phantom reload: no animation, just a cooldown cycle that starts automatically when the mag runs dry (or early via R).")]
    [SerializeField] private float reloadTime      = 2.4f;

    [Header("Gun")]
    [Tooltip("Gun mesh root — must be a CHILD of CamAnchor so it follows lean/yaw. Hidden on the joiner's screen.")]
    [SerializeField] private Transform gunRoot;
    [Tooltip("Barrel end. Bullets spawn here.")]
    [SerializeField] private Transform muzzleTip;
    [Tooltip("Empty under CamAnchor at the hip-fire gun pose. ADS glide target when NOT aiming.")]
    [SerializeField] private Transform hipSocket;
    [Tooltip("Empty under CamAnchor at the ADS gun pose. Move these in the EDITOR to change gun poses — no code.")]
    [SerializeField] private Transform adsSocket;
    [Tooltip("NETWORKED bullet prefab (Rigidbody + SphereCollider + NetworkObject + NetworkTransform + Bullet script). Must also be in NetworkManager's Network Prefabs list.")]
    [SerializeField] private GameObject bulletPrefab;
    [Tooltip("Bullet launch speed in m/s. 80 ≈ visible stream, 150+ ≈ hitscan feel.")]
    [SerializeField] private float bulletSpeed = 80f;

    [Header("Crosshair")]
    [Tooltip("HUD crosshair. Lives in the SCENE, not the prefab — resolved by NAME at spawn (prefabs can't hold scene references).")]
    [SerializeField] private Image crosshair;
    [Tooltip("Color when the center ray is touching anything collidable (walls, enemies).")]
    [SerializeField] private Color crosshairHot = new(1f, 0.25f, 0.25f);

    /// <summary>Replicated lean target: +leanAngle (Q / screen-left), -leanAngle (E), 0 upright.</summary>
    public NetworkVariable<float> LeanRoll = new(0f);

    /// <summary>Owner-replicated for HUDs: 0→1 during the phantom reload, 1 = ready to fire.
    /// Constructor order: (default, readPermission, writePermission) — write goes LAST.</summary>
    public NetworkVariable<float> ReloadProgress = new(
        1f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    private SplitCharacterManager mgr;   // half-ownership — session service, may live in another hierarchy
    private CameraDirector camDir;       // yaw/pitch authority for this half
    private GameOverController gameOver; // for the cursor rule — found lazily, it's a scene Hud object

    private float lastSentTarget;        // last lean target sent to the server (3-state, no RPC spam)
    private float shownRoll;             // locally smoothed lean value driving all visuals

    private float nextShotTime;          // time the next full-auto shot becomes legal
    private float recoil;                // accumulated muzzle rise — drives BOTH the view kick and the sent pitch
    private float reloadEnds;            // time when the phantom reload cycle finishes
    private bool  reloading;             // true while the phantom reload cooldown runs
    private int   ammoInMag = 60;

    // Authored (prefab) base poses, captured at spawn. Lean is applied as a DELTA on top of these,
    // so any authored rotations on the meshes survive — the recurring "authored rotation" trap.
    private Vector3    torsoBaseLocal, camBaseLocal;
    private Quaternion torsoBaseRotLocal, camBaseRotLocal;

    // ---------- HUD-facing getters: read state, never reach into privates ----------
    public int  AmmoInMag   => ammoInMag;
    public int  MagSize     => magSize;
    public bool IsReloading => reloading;

    public override void OnNetworkSpawn()
    {
        mgr    = GetComponentInParent<SplitCharacterManager>();
        camDir = GetComponentInParent<CameraDirector>();

        // capture authored poses ONCE — everything lean-related is a delta from here
        if (torso != null)     { torsoBaseLocal = torso.localPosition;     torsoBaseRotLocal = torso.localRotation; }
        if (camAnchor != null) { camBaseLocal   = camAnchor.localPosition; camBaseRotLocal   = camAnchor.localRotation; }

        ammoInMag = magSize;   // mag size comes from the Inspector, not the field default

        // Prefab assets can't reference scene objects — resolve HUD stuff at runtime.
        // NOTE: GameObject.Find by NAME, not FindWithTag — FindWithTag THROWS if the
        // tag was never created, and an exception here lands in the middle of the
        // joiner's spawn chain (killing the BottomHalf claim and both cameras with it).
        if (crosshair == null)
        {
            GameObject go = GameObject.Find("Crosshair");
            if (go != null) crosshair = go.GetComponent<Image>();
        }
    }

    public override void OnNetworkDespawn()
    {
        mgr = null;

        // Leaving the session (Main Menu / shutdown) must leave a usable cursor —
        // otherwise the pointer stays trapped after the game objects are gone.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    private void Update()
    {
        HandleCursor();   // runs BEFORE the gates: the cursor must react to pause/game-over state

        if (PauseController.SessionPaused) return;   // shared pause blocks ALL character input
        if (!ControlsMyHalf()) return;               // only the owner of the top half runs input

        // muzzle settles back down between bursts (drives view kick AND the sent pitch)
        recoil = Mathf.Lerp(recoil, 0f, recoilRecover * Time.deltaTime);

        // replicated for the joiner's HUD (owner = host = P1, so this write is legal):
        // 1 = ready to fire, climbing 0→1 during the phantom reload
        ReloadProgress.Value = reloading
            ? Mathf.Clamp01(1f - (reloadEnds - Time.time) / reloadTime)
            : 1f;

        // crosshair reacts to whatever the center ray currently touches —
        // client-local cosmetic ray; the server re-decides the real hit per shot
        if (crosshair != null && topCam != null)
        {
            bool overTarget = Physics.Raycast(
                topCam.transform.position, topCam.transform.forward, 50f);
            crosshair.color = overTarget ? crosshairHot : Color.white;
        }

        HandleLean();
        HandleAim();
        HandleFire();
    }

    /// <summary>
    /// Mouse capture: LOCKED to the game while playing; freed the moment the pause
    /// panel (ESC, via PauseController) or the game-over panel is up — both need
    /// real clicks. Cursor state is machine-wide, but both controllers set the SAME
    /// values from SHARED state, so each machine ends up correct.
    /// </summary>
    private void HandleCursor()
    {
        // game-over lives on the per-scene Hud — find it lazily (throttled, cheap scenes)
        if (gameOver == null && Time.frameCount % 30 == 0)
            gameOver = FindObjectOfType<GameOverController>();

        bool freeMouse = PauseController.SessionPaused
                      || (gameOver != null && gameOver.GameOver.Value);

        Cursor.lockState = freeMouse ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible   = freeMouse;
    }

    /// <summary>
    /// Visual pass (runs even for the joiner so they see P1's lean):
    /// torso swings on a bottom hinge in WORLD space about the camera's forward axis;
    /// the camera seat rides the head's actual arc and adds view roll + recoil kick.
    /// </summary>
    private void LateUpdate()
    {
        // snappy, framerate-independent approach to the replicated lean target
        shownRoll = Mathf.Lerp(shownRoll, LeanRoll.Value, 1f - Mathf.Exp(-leanSnap * Time.deltaTime));
        float norm = leanAngle > 0f ? Mathf.Clamp(shownRoll / leanAngle, -1f, 1f) : 0f;

        // Screen axes, ground truth from the camera itself — never assume any local axis
        // is "right" or "forward": authored rotations in the pivot chain killed us before.
        Vector3 right  = topCam != null ? topCam.transform.right   : Vector3.right;  // flattened below for lean
        Vector3 camFwd = topCam != null ? topCam.transform.forward : Vector3.forward;
        Vector3 kickAxis = topCam != null ? topCam.transform.right : Vector3.right;  // FULL right, for recoil pitch
        right.y  = 0f; right.Normalize();                                            // flattened copy for lean roll
        camFwd.y = 0f; camFwd.Normalize();                                           // flattened: looking up/down must not twist the roll axis

        // Lean roll = a WORLD rotation about the camera's own forward axis.
        // Positive shownRoll (Q) → tip the head toward screen-LEFT, with the slide.
        Quaternion rollWorld = Quaternion.AngleAxis(shownRoll, camFwd);
        Vector3 headDelta = Vector3.zero;

        // --- torso: hinge at the bottom, head swings out, base stays planted ---
        if (torso != null)
        {
            // authored base orientation, rebuilt EVERY frame from the current parent
            // (so body yaw keeps flowing through), then tipped in world space
            Quaternion baseWorld = torso.parent != null
                ? torso.parent.rotation * torsoBaseRotLocal : torsoBaseRotLocal;
            Vector3 baseWorldPos = torso.parent != null
                ? torso.parent.TransformPoint(torsoBaseLocal) : torsoBaseLocal;

            // hinge = where the torso meets the legs, expressed in the authored pose
            Vector3 hingeWorld = baseWorldPos + baseWorld * new Vector3(0f, leanHingeY, 0f);

            // rotate the orientation in world space, then reposition so the hinge stays fixed:
            // bottom planted on P2, head arcs out and drops slightly — the R6 look
            torso.rotation = rollWorld * baseWorld;
            torso.position = hingeWorld + rollWorld * (baseWorldPos - hingeWorld);

            // where the head ended up after the swing — this drives the camera seat
            Vector3 headBase = baseWorldPos + baseWorld * new Vector3(0f, leanHeadY, 0f);
            Vector3 headNow  = hingeWorld + rollWorld * (headBase - hingeWorld);
            headDelta = headNow - headBase;
        }

        // --- camera seat: rides the head, leans + recoils about the view axes ---
        // Local-only: nobody else renders this camera. World-space so authored
        // rotations on CamAnchor can't send the slide the wrong way.
        if (camAnchor != null && ControlsMyHalf())
        {
            Vector3 camBaseWorldPos = camAnchor.parent != null
                ? camAnchor.parent.TransformPoint(camBaseLocal) : camBaseLocal;
            camAnchor.position = camBaseWorldPos + headDelta * camFollow;

            Quaternion camBaseWorld = camAnchor.parent != null
                ? camAnchor.parent.rotation * camBaseRotLocal : camBaseRotLocal;

            // view kick: pitch up about the camera's TRUE right axis (not flattened),
            // composed with the lean roll and the authored base — order matters
            camAnchor.rotation = Quaternion.AngleAxis(recoil, kickAxis) *
                                 Quaternion.AngleAxis(shownRoll * 0.25f, camFwd) * camBaseWorld;
        }
    }

    /// <summary>Q/E → 3-state lean target, sent only when the state changes (no RPC spam).</summary>
    private void HandleLean()
    {
        if (Keyboard.current == null) return;

        float target = 0f;
        if (Keyboard.current.qKey.isPressed) target =  leanAngle;
        if (Keyboard.current.eKey.isPressed) target = -leanAngle;

        if (Mathf.Abs(target - lastSentTarget) > 0.01f)
        {
            lastSentTarget = target;
            SubmitLeanServerRpc(target);
        }
    }

    /// <summary>ADS: FOV zoom + viewmodel visibility + editor-driven socket glide. No poses in code.</summary>
    private void HandleAim()
    {
        if (Mouse.current == null) return;
        bool aiming = Mouse.current.rightButton.isPressed;

        // FOV zoom toward the aim/hip value
        if (topCam != null)
            topCam.fieldOfView = Mathf.Lerp(topCam.fieldOfView,
                aiming ? aimFov : hipFov, aimSpeed * Time.deltaTime);

        // Viewmodel: visible only on the machine controlling P1 (the joiner must not see a floating gun)
        if (gunRoot != null && gunRoot.gameObject.activeSelf != ControlsMyHalf())
            gunRoot.gameObject.SetActive(ControlsMyHalf());

        // ADS snap: glide between the two socket poses (move the empties in the editor to retune)
        if (gunRoot != null && hipSocket != null && adsSocket != null)
            gunRoot.localPosition = Vector3.Lerp(gunRoot.localPosition,
                aiming ? adsSocket.localPosition : hipSocket.localPosition,
                aimSpeed * Time.deltaTime);
    }

    /// <summary>
    /// Full-auto with a phantom reload: mag runs dry → automatic cooldown cycle (no animation),
    /// then a fresh mag. R starts the same cycle early.
    /// </summary>
    private void HandleFire()
    {
        if (Mouse.current == null || Keyboard.current == null) return;

        // ---- phantom reload: no animation, just a cooldown cycle ----
        if (!reloading)
        {
            // starts on its own when the mag runs dry, or early with R
            if (ammoInMag <= 0 || Keyboard.current.rKey.wasPressedThisFrame)
            {
                reloading  = true;
                reloadEnds = Time.time + reloadTime;
            }
        }
        else if (Time.time >= reloadEnds)
        {
            reloading  = false;     // cycle finished → mag snaps back to full
            ammoInMag  = magSize;
        }

        if (reloading || ammoInMag <= 0) return;

        // ---- full-auto: one shot per 60/RPM cycle while the trigger is held ----
        if (Mouse.current.leftButton.isPressed && Time.time >= nextShotTime)
        {
            nextShotTime = Time.time + 60f / roundsPerMinute;
            ammoInMag--;

            // climb: each shot raises the muzzle by kickPerShot degrees — this is the
            // value the decay in Update() pulls back down. View kick and the pitch
            // sent to the server both read it, so bullets climb with the muzzle.
            recoil += kickPerShot;

            // the REAL center-screen ray — camera position + camera forward, lean/recoil
            // included. Sent to the server as the aim truth (client-sent aim: standard
            // for co-op; an anti-cheat clamp would live server-side later).
            FireServerRpc(topCam.transform.position, topCam.transform.forward);
        }
    }

    /// <summary>True when this machine controls the top half.</summary>
    private bool ControlsMyHalf()
    {
        return mgr != null && mgr.TopHalfClient.Value == NetworkManager.LocalClientId;
    }

    // ================= server =================

    /// <summary>Validated lean: only the top-half owner may set it.</summary>
    [ServerRpc(RequireOwnership = false)]
    private void SubmitLeanServerRpc(float target, ServerRpcParams p = default)
    {
        if (mgr != null && mgr.TopHalfClient.Value != p.Receive.SenderClientId) return;
        LeanRoll.Value = target;
    }

    /// <summary>
    /// The shot itself. Flow: camera-center ray (aim truth) → intersection point →
    /// a REAL networked projectile from the muzzle converging on that point →
    /// damage decided by Bullet.OnCollisionEnter (server) on impact.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    private void FireServerRpc(Vector3 camPos, Vector3 camFwd, ServerRpcParams p = default)
    {
        if (mgr != null && mgr.TopHalfClient.Value != p.Receive.SenderClientId) return;
        camFwd.Normalize();

        // the camera-center ray, resolved against EVERYTHING collidable — walls,
        // enemies, props — exactly where the crosshair is pointing right now
        Vector3 end = Physics.Raycast(camPos, camFwd, out var hit, 50f)
            ? hit.point
            : camPos + camFwd * 50f;

        // gun trajectory: from the muzzle, converging on that intersection point.
        // This is what makes the camera line and the gun line agree: the gun line's
        // DESTINATION is defined by the camera line's result.
        Vector3 muzzle = muzzleTip != null ? muzzleTip.position : camPos;
        Vector3 flight = (end - muzzle).normalized;

        GameObject go = Instantiate(bulletPrefab, muzzle, Quaternion.LookRotation(flight));

        // the bullet must not collide with the shooter's own body the instant it spawns
        Collider bulletCol = go.GetComponent<Collider>();
        if (bulletCol != null && aimOrigin != null)
            foreach (Collider col in aimOrigin.root.GetComponentsInChildren<Collider>())
                Physics.IgnoreCollision(bulletCol, col);

        go.GetComponent<NetworkObject>().Spawn();                 // replicated to the joiner automatically
        go.GetComponent<Bullet>().Launch(flight, bulletSpeed);    // server-only launch

        // TODO (post-demo): impact FX at 'end'; clamp camPos near the sender's
        // character if anti-cheat hardening is ever wanted.
    }
}