using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Game-over flow for the shared body. The server publishes GameOver when the
/// character's Health hits zero (Health → TriggerGameOver); both clients show
/// the panel from the same replicated state.
/// Play Again / Back to Lobby are server-loaded scenes; Main Menu is a LOCAL
/// disconnect — whoever clicks leaves (host clicking drops everyone).
/// The Hud is per-scene, so the fresh Hud in the next scene resets GameOver.
/// </summary>
public class GameOverController : NetworkBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject panel;              // full-screen "You Died" panel
    [SerializeField] private Button playAgainButton;        // → restart at Level 1
    [SerializeField] private Button lobbyButton;            // → Playground
    [SerializeField] private Button mainMenuButton;         // → disconnect + Main Menu

    [Header("Scenes")]
    [Tooltip("Must match the scene names in Build Settings exactly.")]
    [SerializeField] private string levelOneScene = "Level 1";
    [SerializeField] private string lobbyScene    = "Playground";
    [SerializeField] private string mainMenuScene = "Main Menu";

    /// <summary>Server-owned shared state. New scene → new Hud → false again.</summary>
    public NetworkVariable<bool> GameOver = new(false);

    private bool loading;   // guards double-clicks → double LoadScene

    public override void OnNetworkSpawn()
    {
        if (playAgainButton != null) playAgainButton.onClick.AddListener(OnPlayAgainClicked);
        if (lobbyButton     != null) lobbyButton.onClick.AddListener(OnLobbyClicked);
        if (mainMenuButton  != null) mainMenuButton.onClick.AddListener(OnMainMenuClicked);
    }

    private void Update()
    {
        if (panel != null) panel.SetActive(GameOver.Value);
    }

    /// <summary>Called by Health on the SERVER when the shared body dies.</summary>
    public void TriggerGameOver()
    {
        if (!IsServer) return;
        GameOver.Value = true;
        Time.timeScale = 0f;   // freeze the world behind the panel
    }

    // ---- buttons ----

    private void OnPlayAgainClicked() => RequestSceneServerRpc(levelOneScene);
    private void OnLobbyClicked()     => RequestSceneServerRpc(lobbyScene);

    private void OnMainMenuClicked()
    {
        // LOCAL disconnect: whoever clicks leaves. If the HOST clicks,
        // the shutdown disconnects BOTH players — that's the desired behavior,
        // no extra code needed. Same flow as PauseController's Main Menu.
        if (NetworkManager.Singleton != null) NetworkManager.Singleton.Shutdown();
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuScene);
    }

    // ---- server ----

    [ServerRpc(RequireOwnership = false)]
    private void RequestSceneServerRpc(string sceneName, ServerRpcParams p = default)
    {
        if (!GameOver.Value || loading) return;   // only from a real game-over, once
        loading = true;

        Time.timeScale = 1f;                      // unfreeze for the new scene
        GameOver.Value = false;
        NetworkManager.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        // the Hud dies with the old scene; the new scene's Hud spawns GameOver=false
    }
}