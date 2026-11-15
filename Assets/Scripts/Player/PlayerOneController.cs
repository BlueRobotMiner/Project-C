using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerOneController : NetworkBehaviour
{
    [Header("Lean")]
    [SerializeField] private Transform torso;          // torso CHILD mesh — NOT the root!
    [SerializeField] private float leanRatio = 0.4f;   // lean = pitch × ratio
    [SerializeField] private float maxLeanAngle = 20f; // "a little" forward/back

    [Header("Aim")]
    [SerializeField] private Camera topCam;            // your TopCamera
    [SerializeField] private Transform aimOrigin;      // the TopPivot (yaw is replicated here)
    [SerializeField] private float hipFov = 60f, aimFov = 35f, aimSpeed = 10f;

    public NetworkVariable<float> LeanAngle = new(0f); // server writes, everyone reads

    private SplitCharacterManager mgr;
    private float lastSentLean;
    private CameraDirector camDir;
    public override void OnNetworkSpawn(){
    mgr = GetComponentInParent<SplitCharacterManager>();
    camDir = GetComponentInParent<CameraDirector>();
    }
    public override void OnNetworkDespawn() => mgr = null;

    private void Update()
    {
        // Lean applies on EVERY client (it's replicated state)
        if (torso != null)
            torso.localRotation = Quaternion.Euler(LeanAngle.Value, 0f, 0f);

        if (!ControlsMyHalf()) return;
        HandleLean();
        HandleAim();
        HandleFire();
    }

    private void HandleLean()
    {
        var director = GetComponent<CameraDirector>();
        float pitch = camDir != null ? camDir.TopPitch : 0f;
        float lean = Mathf.Clamp(pitch * leanRatio, -maxLeanAngle, maxLeanAngle);

        if (Mathf.Abs(lean - lastSentLean) > 0.5f)   // skip near-identical updates
        {
            lastSentLean = lean;
            SubmitLeanServerRpc(lean);
        }
    }

    private void HandleAim()
    {
        bool aiming = Mouse.current.rightButton.isPressed;
        if (topCam != null)
            topCam.fieldOfView = Mathf.Lerp(
                topCam.fieldOfView, aiming ? aimFov : hipFov, aimSpeed * Time.deltaTime);
    }

    private void HandleFire()
    {
         if (Mouse.current.leftButton.wasPressedThisFrame)
        FireServerRpc(camDir != null ? camDir.TopPitch : 0f);
    }

    private bool ControlsMyHalf()
    {
        return mgr != null && mgr.TopHalfClient.Value == NetworkManager.LocalClientId;
    }

    [ServerRpc(RequireOwnership = false)]
    private void SubmitLeanServerRpc(float lean, ServerRpcParams p = default)
    {
        if (mgr != null && mgr.TopHalfClient.Value != p.Receive.SenderClientId) return;
        LeanAngle.Value = lean;   // server-authoritative; replicates to all clients
    }

    [ServerRpc(RequireOwnership = false)]
    private void FireServerRpc(float pitch, ServerRpcParams p = default)
    {
        if (mgr != null && mgr.TopHalfClient.Value != p.Receive.SenderClientId) return;

        // Reconstruct full aim: replicated yaw (pivot) + client-passed pitch
        Vector3 dir = aimOrigin.rotation * Quaternion.Euler(pitch, 0f, 0f) * Vector3.forward;

        // Demo shot: 20m hitscan. Swap for a projectile spawn later.
        if (Physics.Raycast(aimOrigin.position, dir, out var hit, 20f))
            Debug.Log($"P1 shot hit {hit.collider.name} at {hit.point}");
    }
}