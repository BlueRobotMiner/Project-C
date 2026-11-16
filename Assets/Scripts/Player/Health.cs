using Unity.Netcode;
using UnityEngine;

public class Health : NetworkBehaviour
{
    [SerializeField] private float max = 100f;
    public NetworkVariable<float> Value = new(100f);
    public float Percent => max > 0f ? Value.Value / max : 1f;

        public override void OnNetworkSpawn()
    {
        if (IsServer) Value.Value = max;
    }

    // Server-authoritative: only the host's call counts
    public void TakeDamage(float amount)
    {
        if (!IsServer) return;
        Value.Value = Mathf.Max(Value.Value - amount, 0f);
        Debug.Log($"[Health] {name} took {amount} → {Value.Value}");   // TEMP — delete after
        if (Value.Value <= 0f) Die();
    }
    
    [Tooltip("Tick on AI/dummies: they despawn on death. Leave OFF on the player character.")]
    [SerializeField] private bool despawnOnDeath;

    private void Die()
    {
        // AI/dummies: remove from the network on both screens. Player: respawn hook later.
        if (despawnOnDeath && TryGetComponent(out NetworkObject no) && no.IsSpawned)
            no.Despawn();
        else
            Debug.Log($"{name} died");
    }
}