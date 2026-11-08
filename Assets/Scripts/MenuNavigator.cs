using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class MenuNavigator : MonoBehaviour
{
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject serverBrowserPanel;
    // [SerializeField] private GameObject creditsPanel;

    [SerializeField] private string prototypeSceneName = "Prototype";   // set to your scene's exact name

    private readonly System.Collections.Generic.Stack<GameObject> history = new();

    void Start() => Show(mainMenuPanel);   // always land on the main menu

    void Update()
    {
        // Esc = Back: upgrades the "hiding/reappearing" chore into one rule
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            Back();
    }

    // ---- wire to Play / Settings / etc. via OnClick ------------------
    public void OnPlay()     { Show(serverBrowserPanel); }
    public void OnSettings() { Show(settingsPanel); }
    public void OnCredits()  { /* Show(creditsPanel); */ }
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

    // ---- core --------------------------------------------------------
    public void Show(GameObject panel)
    {
        if (panel == mainMenuPanel) history.Clear();          // main menu is the root
        else if (panel.activeInHierarchy && history.Count > 0 && history.Peek() == panel) return;

        HideAll();
        panel.SetActive(true);
        history.Push(panel);
    }

    public void Back()
    {
        if (history.Count <= 1) { Show(mainMenuPanel); return; }   // already home
        history.Pop();                                             // drop current
        HideAll();
        history.Peek().SetActive(true);
    }

    void HideAll()
    {
        if (mainMenuPanel)      mainMenuPanel.SetActive(false);
        if (settingsPanel)      settingsPanel.SetActive(false);
        if (serverBrowserPanel) serverBrowserPanel.SetActive(false);
        // if (creditsPanel) creditsPanel.SetActive(false);
    }
}