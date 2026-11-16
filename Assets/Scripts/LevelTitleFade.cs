using System.Collections;
using UnityEngine;

/// <summary>
/// Local cosmetic UI: shows the level title when the scene loads, holds it,
/// then fades it out and deactivates the panel. One copy per scene's HUD.
/// Uses unscaled time so it still fades if someone is paused during the reveal.
/// </summary>
public class LevelTitleFade : MonoBehaviour
{
    [Tooltip("The full-screen title panel (Canvas → Panel → CanvasGroup + TMP text).")]
    [SerializeField] private CanvasGroup panel;
    [Tooltip("Seconds the title stays fully visible before fading.")]
    [SerializeField] private float hold = 1.2f;
    [Tooltip("Seconds the fade-out takes.")]
    [SerializeField] private float fade = 1.5f;

    private void Start()
    {
        panel.alpha = 1f;              // pop up the moment the scene loads
        StartCoroutine(FadeOut());
    }

    private IEnumerator FadeOut()
    {
        yield return new WaitForSeconds(hold);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / fade;
            panel.alpha = 1f - t;
            yield return null;
        }
        panel.gameObject.SetActive(false);   // stop rendering it entirely
    }
}