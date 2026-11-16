using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class Grappling : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private Camera cam;           // P2's BottomCamera
    [SerializeField] private Transform gunTip;     // rope-start point below the camera
    [SerializeField] private LineRenderer lr;
    [SerializeField] private GrapplingMotor motor;

    [Header("Grappling")]
    [SerializeField] private float maxGrappleDistance = 25f;
    [SerializeField] private float overshootYAxis  = 5f;
    [SerializeField] private LayerMask grappleable;

    [Header("Tongue")]
    [SerializeField] private float tongueSpeed  = 30f;   // m/s outward
    [SerializeField] private float retractSpeed = 70f;   // m/s back on a miss

    [Header("Cooldown")]
    [SerializeField] private float grapplingCd = 2.5f;
    private float cdTimer;

    private SplitCharacterManager mgr;
    private bool aiming;

    // replicated tongue state — identical on every client
    private bool ropeOn;
    private bool ropeWillStick;
    private Vector3 ropeTarget;
    private float ropeStartTime;

    public override void OnNetworkSpawn()
    {
        mgr = GetComponentInParent<SplitCharacterManager>();
        if (motor == null) motor = GetComponent<GrapplingMotor>();
        if (lr != null) { lr.enabled = false; lr.positionCount = 2; }
    }

    private void Update()
    {
        if (PauseController.SessionPaused) return;
        if (!ControlsMyHalf()) return;
        if (Mouse.current == null) return;

        if (Mouse.current.rightButton.wasPressedThisFrame) TryStartGrapple();
        if (cdTimer > 0f) cdTimer -= Time.deltaTime;
    }

    private void TryStartGrapple()
    {
        if (aiming || cdTimer > 0f) return;
        aiming = true;

        bool hit = Physics.Raycast(cam.transform.position, cam.transform.forward, out var ray, maxGrappleDistance, grappleable);
        Vector3 target = hit ? ray.point : cam.transform.position + cam.transform.forward * maxGrappleDistance;

        SubmitShootServerRpc(target, hit);   // rope appears + animates on BOTH screens

        if (hit) StartCoroutine(StickRoutine(ray.point));
        else     StartCoroutine(MissRoutine(target));
    }

    // shooter-only timing: when does the tongue reach the surface?
    private IEnumerator StickRoutine(Vector3 point)
    {
        yield return new WaitForSeconds(TravelTime(point));

        SubmitExecuteServerRpc(point);       // tongue landed: freeze + arc launch (server)
        yield return new WaitForSeconds(1f); // stays attached through the flight

        SubmitReleaseServerRpc();
        cdTimer = grapplingCd;
        aiming = false;
    }

    private IEnumerator MissRoutine(Vector3 point)
    {
        float travel = TravelTime(point);
        yield return new WaitForSeconds(travel);                       // full extension
        yield return new WaitForSeconds(travel * tongueSpeed / retractSpeed); // fast retract

        SubmitReleaseServerRpc();
        cdTimer = grapplingCd;
        aiming = false;
    }

    private float TravelTime(Vector3 point) =>
        Vector3.Distance(gunTip.position, point) / tongueSpeed;

    // ---------- server side ----------
    [ServerRpc(RequireOwnership = false)]
    private void SubmitShootServerRpc(Vector3 point, bool willStick, ServerRpcParams p = default)
    {
        if (mgr != null && mgr.BottomHalfClient.Value != p.Receive.SenderClientId) return;
        UpdateRopeClientRpc(true, point, willStick);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SubmitExecuteServerRpc(Vector3 point, ServerRpcParams p = default)
    {
        if (mgr != null && mgr.BottomHalfClient.Value != p.Receive.SenderClientId) return;
        motor.Freeze(true);                  // freeze moved here: at tongue-contact, not at fire
        motor.Execute(point, overshootYAxis);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SubmitReleaseServerRpc(ServerRpcParams p = default)
    {
        if (mgr != null && mgr.BottomHalfClient.Value != p.Receive.SenderClientId) return;
        UpdateRopeClientRpc(false, Vector3.zero, false);
    }

    [ClientRpc]
    private void UpdateRopeClientRpc(bool on, Vector3 point, bool willStick)
    {
        ropeOn = on;
        ropeWillStick = willStick;
        ropeTarget = point;
        ropeStartTime = Time.time;           // each client animates from its own clock
        if (lr != null) lr.enabled = on;
    }

    // Tongue-tip animation — stateless, identical on every client:
    // extends at tongueSpeed; on a hit it holds at the point, on a miss it retracts at retractSpeed
    private void LateUpdate()
    {
        if (lr == null || !lr.enabled || !ropeOn) return;

        float dist    = Vector3.Distance(gunTip.position, ropeTarget);
        float elapsed = Time.time - ropeStartTime;
        float progress;

        if (ropeWillStick)
        {
            progress = Mathf.Clamp01(elapsed * tongueSpeed / Mathf.Max(dist, 0.01f));
        }
        else
        {
            float outTime  = dist / tongueSpeed;
            float backTime = dist / retractSpeed;
            progress = elapsed < outTime
                ? elapsed / Mathf.Max(outTime, 0.01f)
                : 1f - Mathf.Clamp01((elapsed - outTime) / Mathf.Max(backTime, 0.01f));
        }

        Vector3 tip = Vector3.Lerp(gunTip.position, ropeTarget, progress);
        lr.SetPosition(0, gunTip.position);
        lr.SetPosition(1, tip);
    }

    private bool ControlsMyHalf()
    {
        return mgr != null && mgr.BottomHalfClient.Value == NetworkManager.LocalClientId;
    }
}