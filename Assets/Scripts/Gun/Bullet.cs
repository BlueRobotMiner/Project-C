using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Real networked projectile. The SERVER simulates it (velocity + collision +
/// damage); clients replay its transform via NetworkTransform with a kinematic
/// rigidbody. Spawn callback doubles as the gunshot sound event — it runs on
/// every client exactly once per bullet, at the spawn position, for free.
/// </summary>
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

        // Audio (cosmetic): every client hears the shot exactly where the bullet
        // was born — P1's shots AND the AI's, with zero audio networking.
        AudioManager.PlayAt(AudioManager.Sfx.Gunshot, transform.position);
    }

    // server-only, called right after Spawn(). dmgOverride < 0 keeps the prefab's damage.
    public void Launch(Vector3 dir, float speed, float dmgOverride = -1f)
    {
        if (!IsServer) return;
        if (dmgOverride >= 0f) damage = dmgOverride;
        rb.velocity = dir * speed;
        Invoke(nameof(Expire), lifetime);       // misses don't live forever
    }

    private void OnCollisionEnter(Collision c)
    {
        if (!IsServer) return;                  // damage is server-authoritative

        var hp = c.collider.GetComponentInParent<Health>();
        if (hp != null) hp.TakeDamage(damage);

        // Audio: tell every client where the impact landed (cosmetic RPC),
        // then remove the bullet from the network on all screens.
        ImpactClientRpc(c.GetContact(0).point);
        DespawnSelf();
    }

    [ClientRpc]
    private void ImpactClientRpc(Vector3 pos)
    {
        AudioManager.PlayAt(AudioManager.Sfx.BulletImpact, pos);
    }

    private void Expire() => DespawnSelf();

    private void DespawnSelf()
    {
        CancelInvoke(nameof(Expire));
        if (NetworkObject != null && NetworkObject.IsSpawned)
            NetworkObject.Despawn();            // removes it from BOTH screens
    }
}