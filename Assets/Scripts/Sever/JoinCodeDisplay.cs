using TMPro;
using UnityEngine;

public class JoinCodeDisplay : MonoBehaviour
{
    [SerializeField] private TMP_Text hudText;    // HUD canvas label
    [SerializeField] private TMP_Text pauseText;  // Pause menu label

    private string shown;

    private void Update()
    {
        string code = RelaySession.Instance != null ? RelaySession.Instance.JoinCode : null;
        if (string.IsNullOrEmpty(code) || code == shown) return;

        shown = code;
        if (hudText   != null) hudText.text   = $"Join code: {code}";
        if (pauseText != null) pauseText.text = $"Join code: {code}";
    }
}