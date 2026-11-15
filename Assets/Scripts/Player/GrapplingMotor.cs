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
        if (float.IsNaN(velocityToSet.x) || float.IsNaN(velocityToSet.y) || float.IsNaN(velocityToSet.z))
        {
            flyingToPosition = false;
            Frozen = false;
            return;
        }
        flyingToPosition = true;
        rb.velocity = velocityToSet;   // set ONCE — gravity curves it into the arc
        if (Time.frameCount % 1 == 0)   // TEMP — delete after
            Debug.Log($"[Motor] launch v={velocityToSet} from={rb.position} target={targetPos} " + $"kinematic={rb.isKinematic} grav={rb.useGravity}");
    }

    private void FixedUpdate()
    {
                if (Time.frameCount % 30 == 0)  // TEMP — delete after
            Debug.Log($"[Motor] flying v={rb.velocity}");
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
        float g   = -Physics.gravity.y;   // gravity magnitude (positive)
        Vector3 dispXZ = new Vector3(end.x - start.x, 0f, end.z - start.z);
        float dy = end.y - start.y;       // + = target above you

        // Apex must clear the target no matter what — that guarantees every
        // sqrt below gets a non-negative argument (no NaN, ever)
        float apex = Mathf.Max(trajectoryHeight, dy + 0.5f);

        float vy   = Mathf.Sqrt(2f * g * apex);          // launch speed to reach the apex
        float tUp  = Mathf.Sqrt(2f * apex / g);          // time rising to the apex
        float tDown = Mathf.Sqrt(2f * Mathf.Max(apex - dy, 0.01f) / g); // apex → target fall

        return dispXZ / (tUp + tDown) + Vector3.up * vy;
    }
}