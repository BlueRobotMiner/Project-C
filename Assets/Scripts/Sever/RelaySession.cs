using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

public class RelaySession : MonoBehaviour
{
    public static RelaySession Instance { get; private set; }

    public string JoinCode { get; private set; }   // host code, shown on the lobby/pause canvas

    async void Awake()
    {
        Instance = this;
        try
        {
            await UnityServices.InitializeAsync();
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
        catch (Exception e) { UnityEngine.Debug.LogError("Services sign-in failed: " + e.Message); }
    }

    public async System.Threading.Tasks.Task<bool> HostAsync()
    {
        try
        {
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(1);  // 1 slot for P2
            JoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
               transport.SetRelayServerData(
                allocation.RelayServer.IpV4, (ushort)allocation.RelayServer.Port,
                allocation.AllocationIdBytes, allocation.Key, allocation.ConnectionData);

            NetworkManager.Singleton.StartHost();
            return true;
        }
        catch (Exception e) { UnityEngine.Debug.LogError("Host failed: " + e.Message); return false; }
    }

        public async System.Threading.Tasks.Task<bool> JoinAsync(string code, string password = "")
    {
        try
        {
            JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(code.Trim());

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetRelayServerData(
                allocation.RelayServer.IpV4, (ushort)allocation.RelayServer.Port,
                allocation.AllocationIdBytes, allocation.Key, allocation.ConnectionData,
                allocation.HostConnectionData);

            // Password travels to the host inside the connection payload
            NetworkManager.Singleton.NetworkConfig.ConnectionData =
                System.Text.Encoding.UTF8.GetBytes(password ?? "");

            NetworkManager.Singleton.StartClient();
            return true;
        }
        catch (Exception e) { UnityEngine.Debug.LogError("Join failed: " + e.Message); return false; }
    }
}