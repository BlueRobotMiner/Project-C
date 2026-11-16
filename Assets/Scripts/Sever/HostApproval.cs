using System.Text;
using Unity.Netcode;
using UnityEngine;

public class HostApproval : MonoBehaviour
{
    // Set by ServerBrowserPanel.OnCreate when the host clicks Create.
    // Static so any host-side script can set/read it; empty = no password.
    public static string ServerPassword;

    void Start()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            NetworkManager.Singleton.ConnectionApprovalCallback = Approve;
    }

        void Approve(NetworkManager.ConnectionApprovalRequest req,
                 NetworkManager.ConnectionApprovalResponse res)
    {
        // Host checks — the client NEVER self-certifies.
        string sent = (req.Payload == null || req.Payload.Length == 0)
            ? ""
            : Encoding.UTF8.GetString(req.Payload);   // byte[] → string, then compare

        bool ok = string.IsNullOrEmpty(ServerPassword) || sent == ServerPassword;
        
        res.Approved = ok;
        res.CreatePlayerObject = true;
        res.Reason = ok ? null : "Incorrect password or invalid join request";
    }
}