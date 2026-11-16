using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Rigidbody))]
public class Bullet : NetworkBehaviour
{
    [SerializeField] private float damage   = 25f;
    [SerializeField] private float lifetime = 3f;

    private Rigidbody rb;

    public override void OnNetworkSpawn()
    {
        rb = GetComponent<Rigidbody>();
        if (!IsServer) rb.isKinematic = true;   // server simulates; clients replay its transform
    }

    // server-only launch, called right after Spawn(). dmgOverride < 0 keeps the prefab's damage.
    public void Launch(Vector3 dir, float speed, float dmgOverride = -1f)
    {
        if (!IsServer) return;
        if (dmgOverride >= 0f) damage = dmgOverride;
        rb.velocity = dir * speed;
        Invoke(nameof(Expire), lifetime);
    }

    private void OnCollisionEnter(Collision c)
    {
        if (!IsServer) return;                  // damage is server-authoritative

        var hp = c.collider.GetComponentInParent<Health>();
        if (hp != null) hp.TakeDamage(damage);

        DespawnSelf();
    }

    private void Expire() => DespawnSelf();

    private void DespawnSelf()
    {
        CancelInvoke(nameof(Expire));
        if (NetworkObject != null && NetworkObject.IsSpawned)
            NetworkObject.Despawn();            // removes it from BOTH screens
    }
}