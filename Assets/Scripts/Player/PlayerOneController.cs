using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerOneController : NetworkBehaviour
{
    [Header("Lean (Q/E — Rainbow-style)")]
    [SerializeField] private Transform torso;
    [SerializeField] private Transform camAnchor;
    [SerializeField] private float leanAngle  = 15f;   // torso roll degrees
    [SerializeField] private float leanSnap   = 14f;   // 14 ≈ snappy, 18-20 ≈ near-instant
    [SerializeField] private float leanHingeY = -0.5f; // torso-local Y of the hinge (bottom)
    [SerializeField] private float leanHeadY  =  0.35f;// torso-local Y of the head
    [SerializeField] private float camFollow  =  1f;   // 1 = camera rides the head exactly

    [Header("Aim")]
    [SerializeField] private Camera topCam;
    [SerializeField] private Transform aimOrigin;      // LookPivot — supplies the center-fire ray
    [SerializeField] private float hipFov = 60f, aimFov = 35f, aimSpeed = 10f;

    [Header("AK-47")]
    [SerializeField] private float roundsPerMinute = 600f; // full-auto rate
    [SerializeField] private float kickPerShot     = 0.6f; // degrees the muzzle rises per shot
    [SerializeField] private float recoilRecover   = 6f;   // how fast it settles back down
    [SerializeField] private int   magSize         = 30;
    [SerializeField] private float reloadTime      = 2.4f;

    [Header("Gun")]
    [SerializeField] private Transform gunRoot;        // gun mesh root (child of CamAnchor)
    [SerializeField] private Transform muzzleTip;      // barrel end — tracers exit here
    [SerializeField] private Transform hipSocket;      // hip-fire pose (empty under CamAnchor)
    [SerializeField] private Transform adsSocket;      // ADS pose (empty under CamAnchor)
    [SerializeField] private LineRenderer tracer;      // 2-point line, starts disabled

    public NetworkVariable<float> LeanRoll = new(0f);  // +Q (left), -E (right), 0 upright

    private SplitCharacterManager mgr;
    private CameraDirector camDir;
    private float lastSentTarget;
    private float shownRoll;

    private float nextShotTime, recoil, reloadUntil;
    private int   ammoInMag = 30;

    private Vector3    torsoBaseLocal, camBaseLocal;
    private Quaternion torsoBaseRotLocal, camBaseRotLocal;

    public override void OnNetworkSpawn()
    {
        mgr    = GetComponentInParent<SplitCharacterManager>();
        camDir = GetComponentInParent<CameraDirector>();
        if (torso != null)     { torsoBaseLocal = torso.localPosition;         torsoBaseRotLocal = torso.localRotation; }
        if (camAnchor != null) { camBaseLocal   = camAnchor.localPosition;     camBaseRotLocal   = camAnchor.localRotation; }
        ammoInMag = magSize;
    }

    public override void OnNetworkDespawn() => mgr = null;

    private void Update()
    {
        if (PauseController.SessionPaused) return;
        if (!ControlsMyHalf()) return;

        // muzzle settles back down between bursts
        recoil = Mathf.Lerp(recoil, 0f, recoilRecover * Time.deltaTime);

        HandleLean();
        HandleAim();
        HandleFire();
    }

    private void LateUpdate()
    {
        shownRoll = Mathf.Lerp(shownRoll, LeanRoll.Value, 1f - Mathf.Exp(-leanSnap * Time.deltaTime));
        float norm = leanAngle > 0f ? Mathf.Clamp(shownRoll / leanAngle, -1f, 1f) : 0f;

        // screen axes, ground truth from the camera
        Vector3 right  = topCam != null ? topCam.transform.right   : Vector3.right;  // flattened below for lean
        Vector3 camFwd = topCam != null ? topCam.transform.forward : Vector3.forward;
        Vector3 kickAxis = topCam != null ? topCam.transform.right : Vector3.right;  // FULL right, for recoil pitch
        right.y  = 0f; right.Normalize();
        camFwd.y = 0f; camFwd.Normalize();

        Quaternion rollWorld = Quaternion.AngleAxis(shownRoll, camFwd);
        Vector3 headDelta = Vector3.zero;

        // --- torso: hinge at the bottom, head swings out ---
        if (torso != null)
        {
            Quaternion baseWorld = torso.parent != null
                ? torso.parent.rotation * torsoBaseRotLocal : torsoBaseRotLocal;
            Vector3 baseWorldPos = torso.parent != null
                ? torso.parent.TransformPoint(torsoBaseLocal) : torsoBaseLocal;

            Vector3 hingeWorld = baseWorldPos + baseWorld * new Vector3(0f, leanHingeY, 0f);

            torso.rotation = rollWorld * baseWorld;
            torso.position = hingeWorld + rollWorld * (baseWorldPos - hingeWorld);

            // where the head ended up after the swing — this drives the camera
            Vector3 headBase = baseWorldPos + baseWorld * new Vector3(0f, leanHeadY, 0f);
            Vector3 headNow  = hingeWorld + rollWorld * (headBase - hingeWorld);
            headDelta = headNow - headBase;
        }

        // --- camera seat: rides the head, leans + recoils about the view axes ---
        if (camAnchor != null && ControlsMyHalf())
        {
            Vector3 camBaseWorldPos = camAnchor.parent != null
                ? camAnchor.parent.TransformPoint(camBaseLocal) : camBaseLocal;
            camAnchor.position = camBaseWorldPos + headDelta * camFollow;

            Quaternion camBaseWorld = camAnchor.parent != null
                ? camAnchor.parent.rotation * camBaseRotLocal : camBaseRotLocal;
            camAnchor.rotation = Quaternion.AngleAxis(recoil, kickAxis) *
                                 Quaternion.AngleAxis(shownRoll * 0.25f, camFwd) * camBaseWorld;
        }
    }

    private void HandleLean()
    {
        if (Keyboard.current == null) return;

        float target = 0f;
        if (Keyboard.current.qKey.isPressed) target =  leanAngle;
        if (Keyboard.current.eKey.isPressed) target = -leanAngle;

        if (Mathf.Abs(target - lastSentTarget) > 0.01f)   // 3 states → no RPC spam
        {
            lastSentTarget = target;
            SubmitLeanServerRpc(target);
        }
    }

    private void HandleAim()
    {
        if (Mouse.current == null) return;
        bool aiming = Mouse.current.rightButton.isPressed;

        // FOV zoom
        if (topCam != null)
            topCam.fieldOfView = Mathf.Lerp(topCam.fieldOfView,
                aiming ? aimFov : hipFov, aimSpeed * Time.deltaTime);

        // Viewmodel: visible only on the machine controlling P1
        if (gunRoot != null && gunRoot.gameObject.activeSelf != ControlsMyHalf())
            gunRoot.gameObject.SetActive(ControlsMyHalf());

        // ADS snap: glide between the two socket poses (editor-driven — move the empties)
        if (gunRoot != null && hipSocket != null && adsSocket != null)
            gunRoot.localPosition = Vector3.Lerp(gunRoot.localPosition,
                aiming ? adsSocket.localPosition : hipSocket.localPosition,
                aimSpeed * Time.deltaTime);
    }

    private void HandleFire()
    {
        if (Mouse.current == null || Keyboard.current == null) return;

        // R — reload (only when the mag isn't full and nothing's in progress)
        if (Keyboard.current.rKey.wasPressedThisFrame &&
            ammoInMag < magSize && Time.time >= reloadUntil)
            reloadUntil = Time.time + reloadTime;

        if (Time.time < reloadUntil || ammoInMag <= 0) return;

        // full-auto: one shot per cycle while the trigger is held
        if (Mouse.current.leftButton.isPressed && Time.time >= nextShotTime)
        {
            nextShotTime = Time.time + 60f / roundsPerMinute;
            ammoInMag--;
            FireServerRpc((camDir != null ? camDir.TopPitch : 0f) + recoil);
        }
    }

    private bool ControlsMyHalf()
    {
        return mgr != null && mgr.TopHalfClient.Value == NetworkManager.LocalClientId;
    }

    // ---------- server ----------
    [ServerRpc(RequireOwnership = false)]
    private void SubmitLeanServerRpc(float target, ServerRpcParams p = default)
    {
        if (mgr != null && mgr.TopHalfClient.Value != p.Receive.SenderClientId) return;
        LeanRoll.Value = target;
    }

    [ServerRpc(RequireOwnership = false)]
    private void FireServerRpc(float pitch, ServerRpcParams p = default)
    {
        if (mgr != null && mgr.TopHalfClient.Value != p.Receive.SenderClientId) return;

        // Crosshair-accurate: the hit is decided on the camera-CENTER ray,
        // reconstructed from SHARED state (replicated yaw + passed pitch, recoil included)
        Vector3 dir = aimOrigin.rotation * Quaternion.Euler(pitch, 0f, 0f) * Vector3.forward;

        if (Physics.Raycast(aimOrigin.position, dir, out var hit, 50f))
            FireVisualClientRpc(hit.point);
        else
            FireVisualClientRpc(aimOrigin.position + dir * 50f);   // tracer into the distance
    }

    // ---------- visuals (every client) ----------
    [ClientRpc]
    private void FireVisualClientRpc(Vector3 end)
    {
        if (tracer == null || muzzleTip == null) return;

        tracer.SetPosition(0, muzzleTip.position);   // starts at THIS client's gun tip
        tracer.SetPosition(1, end);
        StartCoroutine(TracerFade());
    }

    private IEnumerator TracerFade()
    {
        if (tracer != null) tracer.enabled = true;
        yield return new WaitForSeconds(0.05f);
        if (tracer != null) tracer.enabled = false;
    }
}