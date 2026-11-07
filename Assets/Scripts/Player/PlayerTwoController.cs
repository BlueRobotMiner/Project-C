using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerTwoController : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float jumpForce = 8f;
    [SerializeField] private float superJumpForce = 22f;
    [SerializeField] private float superJumpCooldown = 5f;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private LayerMask groundMask;

    [Header("Grapple")]
    [SerializeField] private float grappleRange = 15f;
    [SerializeField] private float grapplePullSpeed = 20f;
    [SerializeField] private LayerMask grappleMask;
    [SerializeField] private LineRenderer rope;

    private SplitCharacterManager mgr;
    private Rigidbody rb;
    private Vector2 moveInput;
    private bool grappleActive;
    private Vector3 grapplePoint;
    private float superJumpTimer;

    public override void OnNetworkSpawn()
    {
        mgr = GetComponentInParent<SplitCharacterManager>();
        rb = GetComponentInParent<Rigidbody>();   // rb lives on the prefab ROOT
    }

    private void Update()
    {
        if (!ControlsMyHalf()) return;
        if (Keyboard.current == null) return;

        moveInput = new Vector2(
            Keyboard.current.dKey.ReadValue() - Keyboard.current.aKey.ReadValue(), 0f);

        if (Keyboard.current.spaceKey.wasPressedThisFrame && IsGrounded())
            SubmitJumpServerRpc(jumpForce);

        if (Keyboard.current.fKey.wasPressedThisFrame && superJumpTimer <= 0f)
        {
            SubmitJumpServerRpc(superJumpForce);
            superJumpTimer = superJumpCooldown;
        }

        if (Mouse.current.leftButton.wasPressedThisFrame && TryGrapple())
            SubmitGrappleServerRpc(grapplePoint, true);
        if (Mouse.current.leftButton.wasReleasedThisFrame && grappleActive)
            SubmitGrappleServerRpc(grapplePoint, false);

        superJumpTimer -= Time.deltaTime;
    }

    private void FixedUpdate()
    {
        if (!ControlsMyHalf()) return;

        rb.AddForce(new Vector3(moveInput.x, 0f, 0f) * moveSpeed, ForceMode.Force);

        if (grappleActive)
            rb.AddForce((grapplePoint - transform.position).normalized
                        * grapplePullSpeed, ForceMode.Force);
    }

    private bool TryGrapple()
    {
        var cam = Camera.main;
        if (cam == null) return false;
        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        return Physics.Raycast(ray, out var hit, grappleRange, grappleMask)
            && (grapplePoint = hit.point) == grapplePoint;
    }

    private bool IsGrounded()
    {
        if (groundCheck == null) return false;
        return Physics.CheckSphere(groundCheck.position, 0.2f, groundMask);
    }

    private bool ControlsMyHalf()
    {
        if (mgr == null) return false;
        return mgr.BottomHalfClient.Value == NetworkManager.LocalClientId;
    }

    // ---- Server Rpcs: physics stays authoritative on the host ----
    [ServerRpc(RequireOwnership = false)]
    private void SubmitJumpServerRpc(float force, ServerRpcParams p = default)
    {
        if (mgr != null && mgr.BottomHalfClient.Value != p.Receive.SenderClientId) return;
        if (!IsGrounded()) return;
        rb.AddForce(Vector3.up * force, ForceMode.VelocityChange);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SubmitGrappleServerRpc(Vector3 point, bool on, ServerRpcParams p = default)
    {
        if (mgr != null && mgr.BottomHalfClient.Value != p.Receive.SenderClientId) return;

        grappleActive = on;
        grapplePoint = point;

        // Show rope on everyone (visuals are non-authoritative)
        UpdateRopeClientRpc(on, on ? point : Vector3.zero);
    }

    [ClientRpc]
    private void UpdateRopeClientRpc(bool on, Vector3 point)
    {
        grappleActive = on;
        grapplePoint = point;
        rope.enabled = on;
    }

    private void LateUpdate()
    {
        if (rope == null || !grappleActive) return;
        rope.enabled = true;
        rope.SetPositions(new[] { transform.position, grapplePoint });
    }

}