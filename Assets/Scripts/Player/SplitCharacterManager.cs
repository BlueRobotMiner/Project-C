using Unity.Netcode;
using UnityEngine;

public class SplitCharacterManager : NetworkBehaviour
{
    public NetworkVariable<ulong> TopHalfClient    = new NetworkVariable<ulong>(ulong.MaxValue);
    public NetworkVariable<ulong> BottomHalfClient = new NetworkVariable<ulong>(ulong.MaxValue);

    public bool IsControllerOfHalf(bool isTopHalf, ulong clientId) =>
        (isTopHalf ? TopHalfClient.Value : BottomHalfClient.Value) == clientId;

    public bool IsControllerOfAnyHalf(ulong clientId) =>
        IsControllerOfHalf(true, clientId) || IsControllerOfHalf(false, clientId);

    // Assign the first free half to the requesting client
    [ServerRpc(RequireOwnership = false)]
    public void RequestAssignServerRpc(ServerRpcParams p = default)
    {
        var sender = p.Receive.SenderClientId;
        if (IsControllerOfAnyHalf(sender)) return; // already assigned

        if (TopHalfClient.Value == ulong.MaxValue)
            TopHalfClient.Value = sender;
        else if (BottomHalfClient.Value == ulong.MaxValue)
            BottomHalfClient.Value = sender;
        else
            Debug.LogWarning("No free half on this character.");
    }

    // Testing alone: one client controls BOTH halves
    [ServerRpc(RequireOwnership = false)]
    public void RequestSoloControlServerRpc(ServerRpcParams p = default)
    {
        TopHalfClient.Value    = p.Receive.SenderClientId;
        BottomHalfClient.Value = p.Receive.SenderClientId;
    }
}