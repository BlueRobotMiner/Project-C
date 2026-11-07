using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerOneController : NetworkBehaviour
{
    [Header("Top Half")]
    [SerializeField] private Transform torso;
    [SerializeField] private float leanSpeed = 60f;      // degrees per second
    [SerializeField] private float maxLeanAngle = 45f;

    private SplitCharacterManager mgr;
    private float leanInput;

    public override void OnNetworkSpawn()
    {
        mgr = GetComponentInParent<SplitCharacterManager>();
    }

    public override void OnNetworkDespawn() => mgr = null;

    private void Update()
    {
        if (!ControlsMyHalf()) return;
        if (Keyboard.current == null) return;

        leanInput = Keyboard.current.wKey.ReadValue() - Keyboard.current.sKey.ReadValue();
        if (leanInput != 0f) SubmitLeanServerRpc(leanInput * leanSpeed * Time.deltaTime);
    }

    private bool ControlsMyHalf()
    {
        if (mgr == null) return false;
        return mgr.TopHalfClient.Value == NetworkManager.LocalClientId;
    }

    [ServerRpc(RequireOwnership = false)]
    private void SubmitLeanServerRpc(float delta, ServerRpcParams p = default)
    {
        // Server applies lean so both clients stay in sync
        if (mgr != null && mgr.TopHalfClient.Value != p.Receive.SenderClientId) return;

        float current = torso.localEulerAngles.x > 180f
            ? torso.localEulerAngles.x - 360f : torso.localEulerAngles.x;
        torso.localRotation = Quaternion.Euler(
            Mathf.Clamp(current + delta, -maxLeanAngle, maxLeanAngle), 0f, 0f);
    }
}