using Unity.Netcode;
using UnityEngine;

public class GrapplingMotor : NetworkBehaviour
{
    public bool Frozen { get; private set; }   // WASD locked while aiming (tutorial's pm.freeze)
    public bool Flying  => flyingToPosition;

    private Rigidbody rb;
    private Vector3 velocityToSet;
    private Vector3 targetPos;
    private bool flyingToPosition;

    public override void OnNetworkSpawn() => rb = GetComponentInParent<Rigidbody>();

    public void Freeze(bool on) => Frozen = on;    // called server-side only

    // server-only: arc launch toward the grapple point (tutorial math, unchanged)
    public void Execute(Vector3 point, float overshootYAxis)
    {
        if (!IsServer) return;

        Vector3 lowestPoint = new Vector3(rb.position.x, rb.position.y - 1f, rb.position.z);
        float relY = point.y - lowestPoint.y;
        float arc = relY < 0f ? overshootYAxis : relY + overshootYAxis;

        targetPos = point;
        velocityToSet = CalculateJumpVelocity(rb.position, point, arc);
        flyingToPosition = true;
        rb.velocity = velocityToSet;   // set ONCE — gravity curves it into the arc
    }

    private void FixedUpdate()
    {
        if (!IsServer || !flyingToPosition) return;

        if (Vector3.Distance(rb.position, targetPos) < 1.5f)
        {
            flyingToPosition = false;
            Frozen = false;
        }
    }

    // touching anything (ground/wall) ends the flight too — tutorial behaviour
    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer || !flyingToPosition) return;
        flyingToPosition = false;
        Frozen = false;
    }

    private Vector3 CalculateJumpVelocity(Vector3 start, Vector3 end, float trajectoryHeight)
    {
        float gravity = Physics.gravity.y;
        float displacementY = end.y - start.y;
        Vector3 displacementXZ = new Vector3(end.x - start.x, 0f, end.z - start.z);

        Vector3 velocityY  = Vector3.up * Mathf.Sqrt(-2f * gravity * trajectoryHeight);
        Vector3 velocityXZ = displacementXZ / (Mathf.Sqrt(-2f * trajectoryHeight / gravity)
                                              + Mathf.Sqrt(2f * displacementY / gravity));
        return velocityXZ + velocityY;
    }
}