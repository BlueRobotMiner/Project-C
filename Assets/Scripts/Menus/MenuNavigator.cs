using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// One place for menu navigation: every panel (main menu, settings, server browser,
/// credits) is a GameObject toggled here, with a small history stack so Esc/Back
/// walks back through whatever the player opened — never "hide and pray".
///
/// Back buttons: drag each panel's Back button into its slot below and the navigator
/// wires it to Back() at startup. Do NOT also wire those buttons' OnClick to OnBack —
/// one click would then run Back twice and skip a level of history.
public class MenuNavigator : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject serverBrowserPanel;
    [SerializeField] private GameObject creditsPanel;

    [Header("Back Buttons (auto-wired at startup)")]
    [Tooltip("Back button inside the Settings panel → returns to whatever opened it (Main Menu).")]
    [SerializeField] private Button settingsBackButton;
    [Tooltip("Back button inside the Server Browser panel → returns to Main Menu.")]
    [SerializeField] private Button browserBackButton;
    [Tooltip("Back button inside the Credits panel → returns to Main Menu.")]
    [SerializeField] private Button creditsBackButton;

    [Header("Scenes")]
    [SerializeField] private string prototypeSceneName = "PlayGround"; 

    private readonly System.Collections.Generic.Stack<GameObject> history = new();

    void Awake()
    {
        // wire every back button through the SAME rule — Back() handles "no history → main menu"
        Wire(settingsBackButton);
        Wire(browserBackButton);
        Wire(creditsBackButton);
    }

    void Start() => Show(mainMenuPanel);   // always land on the main menu

    void Update()
    {
        // Esc = Back: upgrades the "hiding/reappearing" chore into one rule
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            Back();
    }

    // ---- wire to Play / Settings / Credits / etc. via OnClick ----------
    public void OnPlay()     { Show(serverBrowserPanel); }
    public void OnSettings() { Show(settingsPanel); }
    public void OnCredits()  { Show(creditsPanel); }
    public void OnQuit()     => Application.Quit();
    public void OnBack()     => Back();

    // Play flow's final step later: browser → join/create → load prototype scene
    public void LoadPrototype()
    {
        // Clean up network session first, or a second menu visit breaks
        if (Unity.Netcode.NetworkManager.Singleton != null &&
            Unity.Netcode.NetworkManager.Singleton.IsClient &&
            !Unity.Netcode.NetworkManager.Singleton.IsHost)
            Unity.Netcode.NetworkManager.Singleton.Shutdown();

        SceneManager.LoadScene(prototypeSceneName);
    }

    // ---- core ----------------------------------------------------------
    public void Show(GameObject panel)
    {
        if (panel == null) return;                            // slot not wired yet — do nothing, don't crash

        if (panel == mainMenuPanel) history.Clear();          // main menu is the root
        else if (panel.activeInHierarchy && history.Count > 0 && history.Peek() == panel) return;

        HideAll();
        panel.SetActive(true);
        history.Push(panel);
    }

    public void Back()
    {
        if (history.Count <= 1) { Show(mainMenuPanel); return; }   // already home (or nowhere to go)
        history.Pop();                                             // drop current
        HideAll();
        history.Peek().SetActive(true);
    }

    void HideAll()
    {
        if (mainMenuPanel)      mainMenuPanel.SetActive(false);
        if (settingsPanel)      settingsPanel.SetActive(false);
        if (serverBrowserPanel) serverBrowserPanel.SetActive(false);
        if (creditsPanel)       creditsPanel.SetActive(false);
    }

    //One-liner wiring: the button exists → it backs out exactly like Esc.
    void Wire(Button b)
    {
        if (b != null) b.onClick.AddListener(Back);
    }
}