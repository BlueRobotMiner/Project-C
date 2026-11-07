using Unity.Netcode;   // was: using Netcode;   ← the actual bug
using UnityEngine;

public class CharacterSetup : NetworkBehaviour
{
    void Start()
    {
        // Nothing to do if netcode isn't running (edit-mode tests, etc.)
        if (NetworkManager.Singleton == null || !IsSpawned) return;
        if (NetworkManager.Singleton.IsServer) return; // server assigns via manager, not here

        GetComponent<SplitCharacterManager>().RequestAssignServerRpc();
    }
}