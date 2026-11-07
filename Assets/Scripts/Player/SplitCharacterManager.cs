using Unity.Netcode;
using UnityEngine;

public class SplitCharacterManager : NetworkBehaviour
{
    public NetworkVariable<ulong> TopHalfClient    = new(ulong.MaxValue);
    public NetworkVariable<ulong> BottomHalfClient = new(ulong.MaxValue);

    public bool IsControllerOfHalf(bool isTopHalf, ulong clientId) =>
        (isTopHalf ? TopHalfClient.Value : BottomHalfClient.Value) == clientId;

    public bool IsControllerOfAnyHalf(ulong clientId) =>
        IsControllerOfHalf(true, clientId) || IsControllerOfHalf(false, clientId);

    // Server calls this once both clients are connected
    [ServerRpc(RequireOwnership = false)]
    public void RequestAssignServerRpc(ServerRpcParams p = default)
    {
        var sender = p.Receive.SenderClientId;
        if (IsControllerOfAnyHalf(sender)) return; // already assigned

        if (TopHalfClient.Value == ulong.MaxValue)     TopHalfClient.Value = sender;
        else if (BottomHalfClient.Value == ulong.MaxValue) BottomHalfClient.Value = sender;
        else /* 3rd player trying to grab this body */ Debug.LogWarning("No free half on this character.");
    }

    // Testing alone? Spawn 1 body and grab BOTH halves.
    [ServerRpc(RequireOwnership = false)]
    public void RequestSoloControlServerRpc(ServerRpcParams p = default)
    {
        TopHalfClient.Value = p.Receive.SenderClientId;
        BottomHalfClient.Value = p.Receive.SenderClientId;
    }
}