using UnityEngine;
using UnityEngine.UI;

// Auto-wires click/tick sounds to every Button and Slider under this canvas —
// including the inactive ones (pause/game-over panels). No per-button wiring.
public class UiSounds : MonoBehaviour
{
    private void Start()
    {
        foreach (Button b in GetComponentsInChildren<Button>(true))
            b.onClick.AddListener(() => AudioManager.PlayUi(AudioManager.Sfx.UiClick));

        // throttled so a dragged slider doesn't machine-gun ticks
        foreach (Slider s in GetComponentsInChildren<Slider>(true))
            s.onValueChanged.AddListener(_ =>
            {
                if (Time.unscaledTime >= nextTick)
                {
                    nextTick = Time.unscaledTime + 0.05f;
                    AudioManager.PlayUi(AudioManager.Sfx.UiTick);
                }
            });
    }

    private float nextTick;
}