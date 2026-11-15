using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerTwoController : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float moveAccel = 12f;
    [SerializeField] private float jumpForce = 8f;
    [SerializeField] private float superJumpForce = 22f;
    [SerializeField] private float superJumpCooldown = 5f;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private LayerMask groundMask;

    private SplitCharacterManager mgr;
    private Rigidbody rb;
    private GrapplingMotor grappleMotor;
    private Vector2 moveInput;
    private float superJumpTimer;

    public override void OnNetworkSpawn()
    {
        mgr = GetComponentInParent<SplitCharacterManager>();
        rb = GetComponentInParent<Rigidbody>();
        grappleMotor = GetComponentInParent<GrapplingMotor>();
    }

    private void Update()
    {
        if (!ControlsMyHalf()) return;
        if (Keyboard.current == null) return;

        moveInput = new Vector2(
            Keyboard.current.dKey.ReadValue() - Keyboard.current.aKey.ReadValue(),
            Keyboard.current.wKey.ReadValue() - Keyboard.current.sKey.ReadValue());

        if (Keyboard.current.spaceKey.wasPressedThisFrame && IsGrounded())
            SubmitJumpServerRpc(jumpForce);

        if (Keyboard.current.fKey.wasPressedThisFrame && superJumpTimer <= 0f)
        {
            SubmitJumpServerRpc(superJumpForce);
            superJumpTimer = superJumpCooldown;
        }

        superJumpTimer -= Time.deltaTime;
    }

    private void FixedUpdate()
    {
        if (!ControlsMyHalf()) return;

        // Direction from the SAME yaw that turns P2's camera — what you see is what you walk
        var cam = GetComponent<CameraDirector>();
        float yaw = cam != null ? cam.BotYaw : 0f;
        Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * new Vector3(moveInput.x, 0f, moveInput.y);
        SubmitMoveServerRpc(dir);
    }
    private bool IsGrounded()
    {
        if (groundCheck == null) return false;
        return Physics.CheckSphere(groundCheck.position, 0.2f, groundMask, QueryTriggerInteraction.Ignore);
    }

    private bool ControlsMyHalf()
    {
        if (mgr == null) return false;
        return mgr.BottomHalfClient.Value == NetworkManager.LocalClientId;
    }

    // ---- Server Rpcs: physics stays authoritative on the host ----
    [ServerRpc(RequireOwnership = false)]
    private void SubmitMoveServerRpc(Vector3 dir, ServerRpcParams p = default)
    {
        if (Time.frameCount % 120 == 0)   // TEMP diagnostic — delete after
            Debug.Log($"[P2Move] grounded={IsGrounded()} " +
                 $"frozen={(grappleMotor != null && grappleMotor.Frozen)} " +
                 $"flying={(grappleMotor != null && grappleMotor.Flying)} dir={dir}");
        if (mgr != null && mgr.BottomHalfClient.Value != p.Receive.SenderClientId) return;

        // The grapple owns the body while aiming or mid-arc
        if (grappleMotor != null && (grappleMotor.Frozen || grappleMotor.Flying)) return;
        if (!IsGrounded()) return;   // airborne: jumps/grapple keep their momentum

        if (dir.sqrMagnitude > 1f) dir = dir.normalized;   // no diagonal speed boost

        // Steer horizontal velocity toward the target (no ice, no infinite accel)
        Vector3 vel = rb.velocity;
        float damp = 1f - Mathf.Exp(-moveAccel * Time.fixedDeltaTime);
        vel.x = Mathf.Lerp(vel.x, dir.x * moveSpeed, damp);
        vel.z = Mathf.Lerp(vel.z, dir.z * moveSpeed, damp);
        rb.velocity = vel;
    }

    [ServerRpc(RequireOwnership = false)]
    private void SubmitJumpServerRpc(float force, ServerRpcParams p = default)
    {
        if (mgr != null && mgr.BottomHalfClient.Value != p.Receive.SenderClientId) return;
        if (!IsGrounded()) return;
        rb.AddForce(Vector3.up * force, ForceMode.VelocityChange);
    }
}