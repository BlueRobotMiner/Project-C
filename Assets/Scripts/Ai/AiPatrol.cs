using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Server-authoritative patrol AI — one type, one gun, deliberately minimal.
/// Patrols a loop of waypoint empties; on line-of-sight to the shared player body
/// it stops, faces the player, and fires real networked Bullets at a slow rate.
/// Clients only watch: NetworkTransform replicates the host's simulation.
/// </summary>
public class AiPatrol : NetworkBehaviour
{
    [Header("Patrol")]
    [Tooltip("Empty GameObjects along the route, IN ORDER. The AI loops through them forever.")]
    [SerializeField] private Transform[] waypoints;
    [Tooltip("Walking speed in m/s.")]
    [SerializeField] private float walkSpeed = 2.5f;
    [Tooltip("Close enough to a waypoint to advance to the next one.")]
    [SerializeField] private float arriveDist = 0.4f;
    [Tooltip("How fast the body turns while walking / engaging.")]
    [SerializeField] private float turnSpeed = 8f;

    [Header("Senses")]
    [Tooltip("Empty at head height — the line-of-sight ray starts here.")]
    [SerializeField] private Transform eye;
    [Tooltip("Engagement range. No sight beyond this.")]
    [SerializeField] private float sightRange = 20f;

    [Header("Gun (slower than the AK)")]
    [Tooltip("Same networked Bullet prefab P1 uses — one projectile system, two shooters.")]
    [SerializeField] private GameObject bulletPrefab;
    [Tooltip("Empty at the barrel tip — bullets spawn here.")]
    [SerializeField] private Transform muzzle;
    [Tooltip("Shots per second. 1 = slow, readable menace next to the AK's 10.")]
    [SerializeField] private float shotsPerSecond = 1f;
    [SerializeField] private float bulletSpeed = 40f;
    [Tooltip("Meters of aim scatter at the target — so not every shot connects.")]
    [SerializeField] private float spread = 0.15f;
    [Tooltip("Damage per hit (overrides the Bullet prefab's own damage).")]
    [SerializeField] private float damage = 10f;

    private int wpIndex;          // current patrol waypoint
    private float nextShotTime;   // time the next shot becomes legal
    private Transform playerRoot; // the SHARED body — both halves are one character
    private bool engaged;         // currently has line of sight

    public override void OnNetworkSpawn()
    {
        // Host-only brain: on the joiner this component does nothing at all,
        // the NetworkTransform replays the host's positions.
        if (!IsServer) enabled = false;

        // Session-level lookup (scene service → FindObjectOfType, per project convention)
        SplitCharacterManager m = FindObjectOfType<SplitCharacterManager>();
        if (m != null) playerRoot = m.transform;
    }

    private void Update()
    {
        if (PauseController.SessionPaused) return;   // shared world pause freezes the AI too
        if (playerRoot == null) return;              // character not spawned yet

        engaged = CanSeePlayer();

        if (engaged)
        {
            FacePlayer();
            TryShoot();
        }
        else
        {
            Patrol();   // no sight → back to the route
        }
    }

    /// <summary>Walk the waypoint loop forever: move toward the current one, advance on arrival.</summary>
    private void Patrol()
    {
        if (waypoints == null || waypoints.Length == 0) return;
        Transform target = waypoints[wpIndex];
        if (target == null) return;

        Vector3 to = target.position - transform.position;
        to.y = 0f;                                   // walk on the floor plane only

        if (to.magnitude <= arriveDist)
        {
            wpIndex = (wpIndex + 1) % waypoints.Length;   // loop, no reversing
            return;
        }

        transform.position += to.normalized * walkSpeed * Time.deltaTime;
        FaceDirection(to.normalized);                // walk facing where you walk
    }

    /// <summary>
    /// Line of sight: one ray from the eye to the player's center-mass.
    /// A wall in between = blind. The ray hitting the PLAYER's collider = seen.
    /// </summary>
    private bool CanSeePlayer()
    {
        Vector3 eyePos   = eye != null ? eye.position : transform.position + Vector3.up * 1.5f;
        Vector3 target   = playerRoot.position + Vector3.up * 0.5f;   // center-mass
        Vector3 to       = target - eyePos;

        if (to.magnitude > sightRange) return false;   // too far, regardless of walls

        if (Physics.Raycast(eyePos, to.normalized, out var hit, to.magnitude))
            return hit.collider.transform.root == playerRoot;   // first thing hit: player? seen. Wall? blind.

        return false;   // ray hit nothing at all — treat as blind (shouldn't happen with capsule colliders)
    }

    /// <summary>Turn the body toward the player while engaged.</summary>
    private void FacePlayer()
    {
        Vector3 to = playerRoot.position - transform.position;
        to.y = 0f;
        if (to.sqrMagnitude > 0.001f) FaceDirection(to.normalized);
    }

    private void FaceDirection(Vector3 dir)
    {
        Quaternion look = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, turnSpeed * Time.deltaTime);
    }

    /// <summary>
    /// Slow shots: spawn the shared Bullet prefab from the muzzle, aimed at center-mass
    /// plus scatter, server-side — identical spawn pattern to P1's FireServerRpc.
    /// </summary>
    private void TryShoot()
    {
        if (bulletPrefab == null || muzzle == null) return;
        if (Time.time < nextShotTime) return;
        nextShotTime = Time.time + 1f / Mathf.Max(shotsPerSecond, 0.01f);

        // aim at center-mass with scatter, so the player can dodge and peek
        Vector3 target = playerRoot.position + Vector3.up * 0.5f
                       + Random.insideUnitSphere * spread;
        Vector3 flight = (target - muzzle.position).normalized;

        GameObject go = Instantiate(bulletPrefab, muzzle.position, Quaternion.LookRotation(flight));

        // never shoot ourselves: ignore every collider on this AI's own body
        Collider bulletCol = go.GetComponent<Collider>();
        if (bulletCol != null)
            foreach (Collider c in GetComponentsInChildren<Collider>())
                Physics.IgnoreCollision(bulletCol, c);

        go.GetComponent<NetworkObject>().Spawn();
        go.GetComponent<Bullet>().Launch(flight, bulletSpeed, damage);
    }
}