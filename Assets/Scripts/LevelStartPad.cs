using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;   // LoadSceneMode

/// <summary>
/// Starter pad: when the shared player body walks onto it, the SERVER loads the
/// next level scene. NGO's scene management then pulls the joiner along —
/// clients must never call LoadScene themselves.
/// Lives in the gameplay scene (Playground, and optionally inside Level 1 for a hub feel).
/// </summary>
public class LevelStartPad : NetworkBehaviour
{
    [Header("Level")]
    [Tooltip("Scene to load. Must be added to Build Settings (File → Build Settings → Scenes In Build).")]
    [SerializeField] private string levelSceneName = "Level 1";

    [Tooltip("Seconds to wait after stepping on the pad — gives the title panel time to be seen before the scene switch.")]
    [SerializeField] private float loadDelay = 1.5f;

    private bool fired;   // scene-placed object: resets automatically when the scene reloads

    /// <summary>Server-only: the host simulates the body, so the host's trigger is the truth.</summary>
    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer || fired) return;

        // Only the SHARED CHARACTER triggers the pad — the capsule Rigidbody lives
        // on its root, and SplitCharacterManager sits there too. Bullets/AI won't match.
        Transform root = other.transform.root;
        if (root.GetComponent<SplitCharacterManager>() == null) return;

        fired = true;
        Invoke(nameof(LoadLevel), loadDelay);
    }

    private void LoadLevel()
    {
        // Server-authoritative scene load: NGO syncs every client into the same scene.
        NetworkManager.SceneManager.LoadScene(levelSceneName, LoadSceneMode.Single);
    }
}