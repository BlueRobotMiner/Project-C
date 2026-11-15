using Unity.Netcode;
using TMPro;
using UnityEngine;

public class JoinCodeDisplay : MonoBehaviour
{
    [SerializeField] private TMP_Text hudText;    // HUD canvas label
    [SerializeField] private TMP_Text pauseText;  // Pause menu label

    private string shown;

    private void Update()
    {
        // The code belongs to the HOST's allocation — everyone else sees nothing
        bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

        if (!isHost)
        {
            if (hudText   != null && hudText.gameObject.activeSelf)
                hudText.gameObject.SetActive(false);
            if (pauseText != null && pauseText.gameObject.activeSelf)
                pauseText.gameObject.SetActive(false);
            return;
        }

        string code = RelaySession.Instance != null ? RelaySession.Instance.JoinCode : null;
        if (string.IsNullOrEmpty(code) || code == shown) return;

        shown = code;
        if (hudText   != null) hudText.text   = $"Join code: {code}";
        if (pauseText != null) pauseText.text = $"Join code: {code}";
    }
}