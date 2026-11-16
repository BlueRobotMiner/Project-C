using Unity.Netcode;
using UnityEngine;

public class SpawnPoint : NetworkBehaviour
{
    [Tooltip("Keep the empty at the character's capsule BOTTOM — i.e., on the floor surface, not at head height.")]
    [SerializeField] private bool matchRotation = true;

    private void Start()
    {
        if (!IsServer) return;   // server-side only: clients just receive the NetworkTransform update

        SplitCharacterManager mgr = FindObjectOfType<SplitCharacterManager>();
        if (mgr == null)
        {
            Debug.LogWarning($"[SpawnPoint] No SplitCharacterManager in this scene — spawn skipped.");
            return;
        }

        mgr.transform.position = transform.position;
        if (matchRotation) mgr.transform.rotation = transform.rotation;

        // TEMP — delete after verifying each level spawns at its marker
        Debug.Log($"[SpawnPoint] Character spawned at {transform.position}");
    }
}