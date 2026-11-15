using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerOneController : NetworkBehaviour
{
    [Header("Lean (Q/E — Rainbow-style)")]
    [SerializeField] private Transform torso;
    [SerializeField] private Transform leanPivot;    // LookPivot — supplies the camera-relative direction
    [SerializeField] private Transform camAnchor;    // CamAnchor — the camera's seat (slides with lean)
    [SerializeField] private float leanAngle    = 15f;   // torso roll degrees (visual flavor)
    [SerializeField] private float leanSideDist = 0.35f; // meters the head slides sideways
    [SerializeField] private float leanDropDist = 0.12f; // meters the head sinks while leaning
    [SerializeField] private float leanSnap     = 14f;   // smoothing rate — 14 ≈ snappy, 20+ ≈ instant

    [Header("Aim")]
    [SerializeField] private Camera topCam;
    [SerializeField] private Transform aimOrigin;
    [SerializeField] private float hipFov = 60f, aimFov = 35f, aimSpeed = 10f;
    [SerializeField] private float leanHingeY = -0.5f; // torso-local Y of the hinge (bottom of the capsule)
    [SerializeField] private float leanHeadY  =  0.35f; // torso-local Y of the head (camera height)
    [SerializeField] private float camFollow  =  1f;    // 1 = camera rides the head exactly, 0 = static

    public NetworkVariable<float> LeanRoll = new(0f);    // +left, -right, 0 upright

    private SplitCharacterManager mgr;
    private CameraDirector camDir;
    private float lastSentTarget;
    private float shownRoll;
    private Quaternion torsoBaseRotLocal;
    private Quaternion camBaseRotLocal;

    private Vector3 torsoBaseLocal, camBaseLocal;

    public override void OnNetworkSpawn()
    {
        mgr    = GetComponentInParent<SplitCharacterManager>();
        camDir = GetComponentInParent<CameraDirector>();
        if (torso     != null) { torsoBaseLocal    = torso.localPosition;     torsoBaseRotLocal    = torso.localRotation; }
        if (camAnchor != null) { camBaseLocal      = camAnchor.localPosition; camBaseRotLocal      = camAnchor.localRotation; }
    }

    public override void OnNetworkDespawn() => mgr = null;

    private void Update()
    {
        if (PauseController.SessionPaused) return;
        if (!ControlsMyHalf()) return;
        HandleLean();
        HandleAim();
        HandleFire();
    }

        private void LateUpdate()
    {
        // --- snappy approach to the replicated lean target ---
        shownRoll = Mathf.Lerp(shownRoll, LeanRoll.Value, 1f - Mathf.Exp(-leanSnap * Time.deltaTime));
        float norm = leanAngle > 0f ? Mathf.Clamp(shownRoll / leanAngle, -1f, 1f) : 0f;

        // --- screen axes, ground truth from the camera ---
        Vector3 right  = topCam != null ? topCam.transform.right   : Vector3.right;
        Vector3 camFwd = topCam != null ? topCam.transform.forward : Vector3.forward;
        right.y  = 0f; right.Normalize();
        camFwd.y = 0f; camFwd.Normalize();

        Quaternion rollWorld = Quaternion.AngleAxis(shownRoll, camFwd);
        Vector3 headDelta = Vector3.zero;

        // --- torso: hinge at the bottom, head swings out ---
        if (torso != null)
        {
            Quaternion baseWorld = torso.parent != null
                ? torso.parent.rotation * torsoBaseRotLocal
                : torsoBaseRotLocal;
            Vector3 baseWorldPos = torso.parent != null
                ? torso.parent.TransformPoint(torsoBaseLocal)
                : torsoBaseLocal;

            Vector3 hingeWorld = baseWorldPos + baseWorld * new Vector3(0f, leanHingeY, 0f);

            torso.rotation = rollWorld * baseWorld;
            torso.position = hingeWorld + rollWorld * (baseWorldPos - hingeWorld);

            // where the head ended up after the swing — this drives the camera
            Vector3 headBase = baseWorldPos + baseWorld * new Vector3(0f, leanHeadY, 0f);
            Vector3 headNow  = hingeWorld + rollWorld * (headBase - hingeWorld);
            headDelta = headNow - headBase;
        }

        // --- camera seat: rides the head, rolls about the view axis ---
        if (camAnchor != null && ControlsMyHalf())
        {
            Vector3 camBaseWorldPos = camAnchor.parent != null
                ? camAnchor.parent.TransformPoint(camBaseLocal)
                : camBaseLocal;
            camAnchor.position = camBaseWorldPos + headDelta * camFollow;

            Quaternion camBaseWorld = camAnchor.parent != null
                ? camAnchor.parent.rotation * camBaseRotLocal
                : camBaseRotLocal;
            camAnchor.rotation = Quaternion.AngleAxis(shownRoll * 0.25f, camFwd) * camBaseWorld;
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
        if (topCam != null)
            topCam.fieldOfView = Mathf.Lerp(topCam.fieldOfView,
                aiming ? aimFov : hipFov, aimSpeed * Time.deltaTime);
    }

    private void HandleFire()
    {
        if (Mouse.current == null) return;
        if (Mouse.current.leftButton.wasPressedThisFrame)
            FireServerRpc(camDir != null ? camDir.TopPitch : 0f);
    }

    private bool ControlsMyHalf()
    {
        return mgr != null && mgr.TopHalfClient.Value == NetworkManager.LocalClientId;
    }

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

        Vector3 dir = aimOrigin.rotation * Quaternion.Euler(pitch, 0f, 0f) * Vector3.forward;
        if (Physics.Raycast(aimOrigin.position, dir, out var hit, 20f))
            Debug.Log($"P1 shot hit {hit.collider.name} at {hit.point}");
    }
}