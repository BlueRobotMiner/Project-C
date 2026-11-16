using Unity.Netcode;
using UnityEngine;

public class CharacterSetup : NetworkBehaviour
{
    public override void OnNetworkSpawn()
    {
        Debug.Log($"[CharSetup] spawned on client {NetworkManager.LocalClientId}");  // TEMP
        if (NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer) return;

        var m = GetComponent<SplitCharacterManager>();
        Debug.Log($"[CharSetup] requesting BottomHalf");                            // TEMP
        m.RequestAssignServerRpc();
    }
}