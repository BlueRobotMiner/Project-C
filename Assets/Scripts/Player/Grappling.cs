using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class Grappling : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private Camera cam;           // P2's BottomCamera
    [SerializeField] private Transform gunTip;     // your rope-start point below the camera
    [SerializeField] private LineRenderer lr;
    [SerializeField] private GrapplingMotor motor; // added below, same GameObject

    [Header("Grappling")]
    [SerializeField] private float maxGrappleDistance = 25f;
    [SerializeField] private float grappleDelayTime = 0.25f;
    [SerializeField] private float overshootYAxis  = 5f;
    [SerializeField] private LayerMask grappleable;

    [Header("Cooldown")]
    [SerializeField] private float grapplingCd = 2.5f;
    private float cdTimer;

    private Vector3 grapplePoint;
    private bool aiming;
    private SplitCharacterManager mgr;

    public override void OnNetworkSpawn()
    {
        mgr = GetComponentInParent<SplitCharacterManager>();
        if (motor == null) motor = GetComponent<GrapplingMotor>();
        if (lr != null) { lr.enabled = false; lr.positionCount = 2; }
    }

    private void Update()
    {
        if (!ControlsMyHalf()) return;
        if (Mouse.current == null) return;

        if (Mouse.current.rightButton.wasPressedThisFrame) TryStartGrapple();
        if (cdTimer > 0f) cdTimer -= Time.deltaTime;
    }

    private bool hitSomething;   // NEW: did the shot actually connect?

    private void TryStartGrapple()
    {
        if (aiming || cdTimer > 0f) return;
        aiming = true;

        hitSomething = Physics.Raycast(cam.transform.position, cam.transform.forward,
                                       out var hit, maxGrappleDistance, grappleable);
        grapplePoint = hitSomething ? hit.point
                                    : cam.transform.position + cam.transform.forward * maxGrappleDistance;

        SubmitAimServerRpc(grapplePoint);   // rope appears on BOTH screens (miss = flick to max range)

        // Connect → launch. Miss → rope flicks out and retracts, no pull.
        Invoke(hitSomething ? nameof(ExecuteGrapple) : nameof(StopGrapple), grappleDelayTime);
    }

    private void ExecuteGrapple()
    {
        SubmitExecuteServerRpc(grapplePoint);
        Invoke(nameof(StopGrapple), 1f);                   // tutorial: rope releases ~1s into flight
    }

    private void StopGrapple()
    {
        aiming = false;
        cdTimer = grapplingCd;
        SubmitReleaseServerRpc();
    }

    private bool ControlsMyHalf()
    {
        return mgr != null && mgr.BottomHalfClient.Value == NetworkManager.LocalClientId;
    }

    // ---------- server side ----------
    [ServerRpc(RequireOwnership = false)]
    private void SubmitAimServerRpc(Vector3 point, ServerRpcParams p = default)
    {
        if (mgr != null && mgr.BottomHalfClient.Value != p.Receive.SenderClientId) return;
        grapplePoint = point;
        motor.Freeze(true);
        UpdateRopeClientRpc(true, point);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SubmitExecuteServerRpc(Vector3 point, ServerRpcParams p = default)
    {
        if (mgr != null && mgr.BottomHalfClient.Value != p.Receive.SenderClientId) return;
        motor.Execute(point, overshootYAxis);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SubmitReleaseServerRpc(ServerRpcParams p = default)
    {
        if (mgr != null && mgr.BottomHalfClient.Value != p.Receive.SenderClientId) return;
        motor.Freeze(false);
        UpdateRopeClientRpc(false, Vector3.zero);
    }

    [ClientRpc]
    private void UpdateRopeClientRpc(bool on, Vector3 point)
    {
        grapplePoint = point;
        if (lr != null) lr.enabled = on;
    }

    // rope endpoints tracked every frame on every client;
    // [0] = your gunTip (below the camera), [1] = the shared grapple point
    private void LateUpdate()
    {
        if (lr == null || !lr.enabled) return;
        lr.SetPosition(0, gunTip.position);
        lr.SetPosition(1, grapplePoint);
    }
}