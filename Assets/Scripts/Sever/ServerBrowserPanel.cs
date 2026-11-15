using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class ServerBrowserPanel : MonoBehaviour
{
    [Header("List")]
    [SerializeField] private RectTransform content;          // ScrollView/Viewport/Content
    [SerializeField] private ServerRow template;             // Content/ServerPanel

    [Header("Password pop-up")]
    [SerializeField] private GameObject passwordPopup;       // Password pop-upPanel
    [SerializeField] private TMP_Text popupLabel;
    [SerializeField] private TMP_Text errorText;             // "ERROR" Text (TMP)
    [SerializeField] private TMP_InputField passwordInput;
    [SerializeField] private Button submitButton, backButton;

    [Header("Host a lobby")]
    [SerializeField] private TMP_InputField createNameInput;
    [SerializeField] private Toggle     privateToggle;
    [SerializeField] private TMP_InputField hostPasswordInput;
    [SerializeField] private Button     createButton;

    [Header("Join a lobby")]
    [SerializeField] private TMP_InputField codeInput;
    [SerializeField] private Button     joinCodeButton;
    [SerializeField] private TMP_Text   joinStatusText;

    [Header("Connection")]
    [SerializeField] private RelaySession relay;

    RectTransform templateRect;
    System.Action onValidPassword;
    string pendingJoinCode;   // kept for the password retry after a refused attempt

    void Awake()
    {
        templateRect = template.GetComponent<RectTransform>();
        template.gameObject.SetActive(false);                // template never displays

        passwordPopup.SetActive(false);
        errorText.text     = "";
        joinStatusText.text = "";
        passwordInput.contentType     = TMP_InputField.ContentType.Password;
        hostPasswordInput.contentType = TMP_InputField.ContentType.Password;

        submitButton.onClick.AddListener(OnSubmit);
        backButton .onClick.AddListener(() => passwordPopup.SetActive(false));
        createButton.onClick.AddListener(OnCreate);
        joinCodeButton.onClick.AddListener(OnJoinByCode);
        passwordInput.onSubmit.AddListener(_ => OnSubmit());

        // Fallback: find the RelaySession singleton if the Inspector slot was left empty
        if (relay == null) relay = RelaySession.Instance != null
            ? RelaySession.Instance
            : FindObjectOfType<RelaySession>();
        if (relay == null)
            Debug.LogError("ServerBrowserPanel: no RelaySession in scene — add one to the NetworkManager object.");

        // Refused connection = the host requires a password (or closed the game).
        // The popup only appears HERE — never before a connection attempt.
                if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientDisconnectCallback += clientId =>
            {
                if (string.IsNullOrEmpty(pendingJoinCode)) return;   // only react to join attempts

                joinStatusText.text = "Connection refused by host.";
                AskPassword("Connection refused — this game requires a password " +
                            "(or the host closed it). Enter the password below, then Join again.",
                    () => _ = relay.JoinAsync(pendingJoinCode, passwordInput.text));
            };
    }

    void OnEnable()
    {
        if (!Application.isPlaying) return;
        Refresh();
    }

    // ================= demo-only list =================
    public void Refresh()
    {
        foreach (Transform c in content)
            if (c != templateRect) Destroy(c.gameObject);

        var mock = new (string n, string players, string ping, bool priv, bool pw)[]
        {
            ("Zach's server",  "1/2", "32ms",  false, false),
            ("Funhouse Co-op", "2/2", "58ms",  true,  false),
            ("Grapple Guys",   "0/2", "101ms", true,  true),
            ("Open LAN test",  "0/2", "5ms",   false, true),
        };

        for (int i = 0; i < mock.Length; i++)
        {
            var m = mock[i];
            var row = Instantiate(template, content);
            row.gameObject.SetActive(true);
            row.Init(m.n, m.players, m.ping, m.priv, m.pw, i,
                () => joinStatusText.text = "Demo build — lobbies come in the full version.");
        }
    }

    // ================= join flow (real, via Relay) =================
    void OnJoinByCode()
    {
        string code = codeInput.text.Trim();
        if (string.IsNullOrEmpty(code)) { joinStatusText.text = "Enter a join code."; return; }

        pendingJoinCode = code;
        joinStatusText.text = "Connecting…";
        _ = relay.JoinAsync(code, "");   // try as an open game first — popup only if refused
    }

    // ================= host flow (real, via Relay) =================
    async void OnCreate()
    {
        string name = createNameInput.text.Trim();
        if (string.IsNullOrEmpty(name)) { joinStatusText.text = "Enter a server name first."; return; }

        HostApproval.ServerPassword = hostPasswordInput.text;   // empty string = open game

        bool hosted = await relay.HostAsync();
        if (!hosted) { joinStatusText.text = "Host failed — see console."; return; }

        joinStatusText.text = $"Hosted '{name}' — Code: {relay.JoinCode}";

        NetworkManager.Singleton.SceneManager.LoadScene("Playground", LoadSceneMode.Single);
    }

    // ================= password popup =================
    void AskPassword(string prompt, System.Action onValid)
    {
        popupLabel.text     = "Please enter the password below";
        errorText.color     = Color.white;
        errorText.text      = prompt;
        passwordInput.text  = "";
        passwordPopup.SetActive(true);
        passwordInput.ActivateInputField();
        onValidPassword = onValid;
    }

    void OnSubmit()
    {
        if (onValidPassword == null) { passwordPopup.SetActive(false); return; }
        passwordPopup.SetActive(false);
        onValidPassword.Invoke();
        onValidPassword = null;
    }
}