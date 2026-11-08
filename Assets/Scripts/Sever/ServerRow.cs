using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ServerRow : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText, playersText, pingText, passwordText;
    [SerializeField] private GameObject lockIcon, unlockIcon;
    [SerializeField] private Button joinButton;
    [SerializeField] private Image background;

    public string ServerName   { get; private set; }
    public bool   HasPassword  { get; private set; }
    public string JoinCode     { get; private set; }   // real code later via Relay

    public void Init(string name, string players, string ping,
                     bool isPrivate, bool hasPassword, int rowIndex,
                     UnityEngine.Events.UnityAction onClick)
    {
        ServerName  = name;
        HasPassword = hasPassword;
        JoinCode    = name;                       // placeholder until Relay codes exist

        nameText.text     = name;
        playersText.text  = players;
        pingText.text     = ping;
        passwordText.text = hasPassword ? "yes" : "no";

        if (lockIcon   != null) lockIcon.SetActive(isPrivate);
        if (unlockIcon != null) unlockIcon.SetActive(!isPrivate);

        background.color = (rowIndex % 2 == 0)
            ? new Color(1f, 1f, 1f, 0.10f)
            : new Color(0f, 0f, 0f, 0.06f);

        joinButton.onClick.RemoveAllListeners();
        joinButton.onClick.AddListener(onClick);
    }
}