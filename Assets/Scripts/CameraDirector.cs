using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraDirector : NetworkBehaviour
{
    [Header("Pivots (children of the halves)")]
    [SerializeField] private Transform topPivot;
    [SerializeField] private Transform bottomPivot;
    [SerializeField] private Camera topCam;
    [SerializeField] private Camera bottomCam;

    [Header("Look")]
    [SerializeField] private float sensitivity = 2f;
    [SerializeField] private Transform topBody;    // mesh of top half — rotates in yaw
    [SerializeField] private Transform bottomBody;

    private SplitCharacterManager mgr;
    private Transform activePivot;
    private float topYaw, topPitch, botYaw, botPitch;
    private bool usingTop;

    public override void OnNetworkSpawn()
    {
        mgr = GetComponentInParent<SplitCharacterManager>();

        // Solo / fallback: if you own both halves, toggle with Tab; else your half decides
        if (IsMyHalf(true) && !IsMyHalf(false)) usingTop = true;
        else if (IsMyHalf(false) && !IsMyHalf(true)) usingTop = false;
    }

    private void Update()
    {
        // 1) Pick which camera this client renders
        bool canTop    = IsMyHalf(true);
        bool canBottom = IsMyHalf(false);

        if (canTop && canBottom && Keyboard.current.tabKey.wasPressedThisFrame)
            usingTop = !usingTop;

        bool camTop = canTop && (usingTop || !canBottom);
        activePivot = camTop ? topPivot : bottomPivot;
        topCam.enabled = camTop && canTop;
        bottomCam.enabled = !camTop && canBottom;

        if (activePivot == null || Mouse.current == null) return;

        // 2) Local pitch + collect yaw delta
        // d.x = horizontal motion → yaw (turns the body, replicated)
        // d.y = vertical motion   → pitch (camera only, local)
        Vector2 d = Mouse.current.delta.ReadValue()
            * (SettingsManager.Instance == null ? 2f : SettingsManager.Instance.MouseSensitivity)
            * Time.deltaTime;
        float yawDelta = d.x;

        // pitch is purely local — nobody else needs to know it
        if (camTop) { topPitch = Mathf.Clamp(topPitch - d.y, -60f, 60f); }
        else        { botPitch = Mathf.Clamp(botPitch - d.y, -60f, 60f); }

        // 3) Yaw rotates the shared body → server applies it for everyone
        if (camTop)
        {
            topYaw += yawDelta;
            SubmitYawServerRpc(true, topYaw);
        }
        else
        {
            botYaw += yawDelta;
            SubmitYawServerRpc(false, botYaw);
        }
    }

    private bool IsMyHalf(bool top)
    {
        if (mgr == null) return false;
        ulong me = NetworkManager.LocalClientId;
        return top ? mgr.TopHalfClient.Value == me : mgr.BottomHalfClient.Value == me;
    }

    [ServerRpc(RequireOwnership = false)]
    private void SubmitYawServerRpc(bool top, float yaw, ServerRpcParams p = default)
    {
        if (mgr != null && mgr.IsControllerOfHalf(top, p.Receive.SenderClientId) == false) return;

        if (top)
        {
            topYaw = yaw;
            topPivot.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }
        else
        {
            botYaw = yaw;
            bottomPivot.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }
    }

    // Local-only pitch so the camera tilts up/down without touching the body
    private void LateUpdate()
    {
        if (activePivot == null) return;
        if (activePivot == topPivot)  topPivot.localRotation =
            Quaternion.Euler(topPitch, topYaw, 0f);
        else                          bottomPivot.localRotation =
            Quaternion.Euler(botPitch, botYaw, 0f);
    }
}