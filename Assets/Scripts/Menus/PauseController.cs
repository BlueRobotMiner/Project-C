using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PauseController : NetworkBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private TMP_Text  pausedByText;
    [SerializeField] private GameObject settingsPanel;

    [Header("Buttons")]
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private Button quitButton;

    public NetworkVariable<ulong> PauserId = new(ulong.MaxValue);
    public NetworkVariable<bool>  SettingsOpen = new(false);
    public NetworkVariable<ulong> SettingsUserId = new(ulong.MaxValue);

    public static bool SessionPaused { get; private set; }

    private SplitCharacterManager mgr;

    void Awake()
    {
        if (resumeButton   != null) resumeButton.onClick.AddListener(() => RequestUnpauseServerRpc());
        if (settingsButton != null) settingsButton.onClick.AddListener(() =>
        {
            if (settingsPanel != null) settingsPanel.SetActive(true);
            RequestSettingsServerRpc(true);          // publish the lock to the server
        });
        if (mainMenuButton != null) mainMenuButton.onClick.AddListener(LeaveToMenu);
        if (quitButton     != null) quitButton.onClick.AddListener(() => Application.Quit());
    }

       public override void OnNetworkSpawn()
    {
        // The Hud is NOT a child of the character — GetComponentInParent
        // can't see the manager from here. Look up the scene's manager instead.
        mgr = GetComponentInParent<SplitCharacterManager>();
        if (mgr == null) mgr = FindObjectOfType<SplitCharacterManager>();
    }

    private void Update()
    {
        SessionPaused = PauserId.Value != ulong.MaxValue;
        bool paused = SessionPaused;

        if (pausePanel != null) pausePanel.SetActive(paused);
        Time.timeScale = paused ? 0f : 1f;

        if (paused && pausedByText != null)
        {
            string msg = $"Waiting for {NameOf(PauserId.Value)} to Unpause";
            if (SettingsOpen.Value)
                msg += $"\n{nameof(NameOf).Replace(nameof(NameOf), "")}" +
                       $"Player {SettingsUserId.Value} is in Settings — can't unpause";
            pausedByText.text = msg;
        }
        if (paused && pausedByText != null && SettingsOpen.Value && mgr != null)
            pausedByText.text = $"Waiting for {NameOf(PauserId.Value)} to Unpause\n" +
                                $"{NameOf(SettingsUserId.Value)} is in Settings";

        if (resumeButton != null)
            resumeButton.interactable = paused && !SettingsOpen.Value &&
                                        PauserId.Value == NetworkManager.LocalClientId;

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            // ESC backs out of Settings FIRST — it never touches the pause state
            if (settingsPanel != null && settingsPanel.activeSelf)
            {
                settingsPanel.SetActive(false);
                RequestSettingsServerRpc(false);     // release the lock
                return;
            }

            if (!paused)                                            RequestPauseServerRpc();
            else if (PauserId.Value == NetworkManager.LocalClientId) RequestUnpauseServerRpc();
        }
    }

    private string NameOf(ulong id)
    {
        if (mgr == null) return "Player";
        if (mgr.TopHalfClient.Value    == id) return "Player 1";
        if (mgr.BottomHalfClient.Value == id) return "Player 2";
        return $"Player {id}";
    }

    private void LeaveToMenu()
    {
        if (NetworkManager.Singleton != null) NetworkManager.Singleton.Shutdown();
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene("Main Menu");
    }

    // ---- server-owned state ----
    [ServerRpc(RequireOwnership = false)]
    private void RequestPauseServerRpc(ServerRpcParams p = default)
    {
        if (PauserId.Value != ulong.MaxValue) return;
        PauserId.Value = p.Receive.SenderClientId;
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestUnpauseServerRpc(ServerRpcParams p = default)
    {
        if (SettingsOpen.Value) return;                      // settings lock refuses unpause
        if (PauserId.Value != p.Receive.SenderClientId) return;
        PauserId.Value = ulong.MaxValue;
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestSettingsServerRpc(bool open, ServerRpcParams p = default)
    {
        if (SettingsOpen.Value && open && SettingsUserId.Value != p.Receive.SenderClientId)
            return;                                          // one settings user at a time
        SettingsOpen.Value   = open;
        SettingsUserId.Value = open ? p.Receive.SenderClientId : ulong.MaxValue;
    }
}