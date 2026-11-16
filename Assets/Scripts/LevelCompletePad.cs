using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Green level-complete pad: the SERVER loads the next level. Gate: every AI in
/// the scene must be dead (UNTICK 'Require All Enemies Dead' to test freely).
/// Flow: step on pad → "Level Complete" panel pops on both screens with the
/// completion time (MM:SS) → unscaled hold (the world is FROZEN behind it) →
/// server loads the next scene. Tick 'Triggers Game Over' on the LAST level's
/// pad to end on the Game Over panel (victory menu) instead of a scene load.
/// </summary>
public class LevelCompletePad : NetworkBehaviour
{
    [Header("Level")]
    [Tooltip("Scene to load after the panel. Must be in Build Settings. Last level can loop to 'Playground' or a Victory scene.")]
    [SerializeField] private string nextSceneName = "Level 2";

    [Tooltip("Seconds the 'Level Complete' panel stays up before the scene switch.")]
    [SerializeField] private float loadDelay = 2.5f;

    [Header("Gate")]
    [Tooltip("Require every AI in the scene to be dead before the pad works. UNTICK FOR TESTING — then the pad completes with enemies still alive.")]
    [SerializeField] private bool requireAllEnemiesDead = true;

    [Header("Finale")]
    [Tooltip("Tick on the LAST level's pad: instead of loading the next scene, pop the Game Over panel after the complete panel. Its buttons become the victory menu (Continue → Level 1, Lobby → Playground, Main Menu → disconnect).")]
    [SerializeField] private bool triggersGameOver;

    [Header("UI")]
    [Tooltip("Full-screen 'Level Complete' panel in this scene's HUD. Starts inactive.")]
    [SerializeField] private GameObject completePanel;
    [Tooltip("TMP text on the panel showing the completion time, e.g. 'Completed in 02:35'.")]
    [SerializeField] private TMP_Text timeText;

    /// <summary>Static freeze flag while the complete panel holds the screen
    /// (PauseController's InputBlocked reads it — single clock-writer rule).</summary>
    public static bool Frozen { get; private set; }

    private bool fired;

    public override void OnNetworkSpawn() => Frozen = false;   // fresh scene → unfrozen

    /// <summary>Server-only: the host simulates the body, so the host's trigger is the truth.</summary>
    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer || fired) return;

        // only the shared character triggers the pad (capsule Rigidbody on its root)
        if (other.transform.root.GetComponent<SplitCharacterManager>() == null) return;

        // GATE: level complete only when no AI remains alive — skippable while testing.
        // Despawned AI GameObjects are destroyed, so FindObjectsOfType counts only the living.
        if (requireAllEnemiesDead && FindObjectsOfType<AiPatrol>().Length > 0) return;

        fired = true;

        // Completion time: seconds since THIS scene finished loading, captured ONCE on
        // the server and shipped through the RPC — both screens show the same number.
        int total = Mathf.FloorToInt(Time.timeSinceLevelLoad);
        string mmss = $"{total / 60:00}:{total % 60:00}";
        ShowCompleteClientRpc(mmss);

        // UNSCALED hold — Invoke uses scaled time, which would deadlock at timeScale 0.
        StartCoroutine(CompleteHold(loadDelay));
    }

    private System.Collections.IEnumerator CompleteHold(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);   // UNSCALED — world is frozen
        LoadNext();
    }

    [ClientRpc]
    private void ShowCompleteClientRpc(string mmss)
    {
        Frozen = true;                                  // hold the world while the panel is up
        if (completePanel != null) completePanel.SetActive(true);
        if (timeText != null) timeText.text = $"Completed in {mmss}";
    }

    private void LoadNext()
    {
        Time.timeScale = 1f;   // defensive: never carry a frozen clock into the next level

        // Finale: no next scene — hand control to the Game Over panel as the victory menu.
        // TriggerGameOver freezes the world and replicates the panel to both screens.
        if (triggersGameOver)
        {
            GameOverController gameOver = FindObjectOfType<GameOverController>();
            if (gameOver != null)
            {
                gameOver.TriggerGameOver();
                return;
            }
            Debug.LogWarning($"[LevelCompletePad] 'Triggers Game Over' is ticked but no GameOverController in this scene — falling back to scene load.");
        }

        NetworkManager.SceneManager.LoadScene(nextSceneName, LoadSceneMode.Single);
    }
}