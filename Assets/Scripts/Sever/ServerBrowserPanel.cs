using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ServerBrowserPanel : MonoBehaviour
{
    [Header("List")]
    [SerializeField] private RectTransform content;          // ScrollView/Viewport/Content
    [SerializeField] private ServerRow template;             // Content/ServerPanel

    [Header("Password pop-up")]
    [SerializeField] private GameObject passwordPopup;       // Password pop-upPanel
    [SerializeField] private TMP_Text popupLabel;            // its top Text (TMP) — reuse for title
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

    private RectTransform templateRect;
    private System.Action onValidPassword;

    void Awake()
    {
        templateRect  = template.GetComponent<RectTransform>();
        template.gameObject.SetActive(false);                // the example never shows

        passwordPopup.SetActive(false);
        errorText.text     = "";
        joinStatusText.text = "";
        passwordInput.contentType     = TMP_InputField.ContentType.Password;
        hostPasswordInput.contentType = TMP_InputField.ContentType.Password;

        submitButton.onClick.AddListener(OnSubmit);
        backButton .onClick.AddListener(() => passwordPopup.SetActive(false));
        createButton.onClick.AddListener(OnCreate);
        joinCodeButton.onClick.AddListener(OnJoinByCode);
        passwordInput.onSubmit.AddListener(_ => OnSubmit());   // Enter submits
    }

    void OnEnable() { if (!Application.isPlaying) return; Refresh(); }

    // ================= list =================
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
            row.Init(m.n, m.players, m.ping, m.priv, m.pw, i, () => JoinRow(row));
        }
    }

    // ================= join flows =================
    public void JoinRow(ServerRow row)
    {
        if (row.HasPassword)
            AskPassword($"Password required to join {row.ServerName}",
                        () => Connect(row.ServerName, passwordInput.text));
        else
            Connect(row.ServerName, null);
    }

    void OnJoinByCode()
    {
        string code = codeInput.text.Trim();
        if (string.IsNullOrEmpty(code)) { joinStatusText.text = "Enter a join code."; return; }

        // Mock lookup — Unity Lobby later resolves code → lobby data
        if (code == "ZACH")            Connect(code, null);
        else if (code == "ZACH-LOCKED") AskPassword("This server requires a password",
                        () => Connect(code, passwordInput.text));
        else                            joinStatusText.text = "No server with that code.";
    }

    void Connect(string target, string password)
    {
        JoinPayload.Password    = password;
        JoinPayload.HasPassword = !string.IsNullOrEmpty(password);
        NetworkManager.Singleton.NetworkConfig.ConnectionData =
            System.Text.Encoding.UTF8.GetBytes(password ?? "");
        NetworkManager.Singleton.StartClient();
        joinStatusText.text = $"Connecting to {target}...";
    }

    // ================= host flow =================
    void OnCreate()
    {
        string name = createNameInput.text.Trim();
        if (string.IsNullOrEmpty(name)) { ShowPopupInfo("Enter a server name first."); return; }

        HostApproval.ServerPassword = hostPasswordInput.text;    // empty = none
        NetworkManager.Singleton.StartHost();
        // TODO: load lobby scene, pause menu shows JoinCode
        joinStatusText.text = $"Hosted '{name}'{(privateToggle.isOn ? " (private)" : "")}";
    }

    // ================= password popup =================
    void AskPassword(string prompt, System.Action onValid)
    {
        popupLabel.text  = "Please enter the password below";
        errorText.color  = Color.white;
        errorText.text   = prompt;
        passwordInput.text  = "";
        passwordPopup.SetActive(true);
        passwordInput.ActivateInputField();
        onValidPassword = onValid;
    }

    void ShowPopupInfo(string msg) => AskPassword(msg, null);    // info reuse; no password needed → Back closes

    void OnSubmit()
    {
        // Mock check only. The REAL, uncheatable check is HostApproval on the host at connect time.
        if (onValidPassword == null) { passwordPopup.SetActive(false); return; }

        if (passwordInput.text == "swordfish")                    // matches the mock servers
        {
            passwordPopup.SetActive(false);
            onValidPassword.Invoke();
            onValidPassword = null;
        }
        else
        {
            errorText.color  = Color.red;
            errorText.text   = "Incorrect password — try again.";
            passwordInput.text  = "";
            passwordInput.ActivateInputField();
        }
    }
}