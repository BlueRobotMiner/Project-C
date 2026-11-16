using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Player HUD wiring: bottom-left health slider (replicated Health state) and the
/// right-side reload slider (owner-replicated gun state). Lives on the SCENE canvas,
/// so the Slider/Text slots are drag-and-drop. Character references are resolved
/// lazily — the character spawns after this canvas exists.
/// </summary>
public class PlayerHud : MonoBehaviour
{
    [Header("Bars (Sliders)")]
    [Tooltip("Health slider. Set Max Value = 1 (or uncheck Whole Numbers) — the script drives a 0..1 value.")]
    [SerializeField] private Slider healthSlider;
    [Tooltip("Reload slider — grows 0→1 during the phantom reload; full = ready to fire. Set Max Value = 1.")]
    [SerializeField] private Slider reloadSlider;

    [Header("Optional")]
    [Tooltip("Ammo counter text, e.g. '60 / 60'. Leave empty to skip.")]
    [SerializeField] private TMP_Text ammoText;

    private Health health;               // shared body's health — replicated, readable everywhere
    private PlayerOneController p1;      // gun state — reload progress replicated by the owner

    private void Update()
    {
        // lazy lookup until the character exists (it spawns after the menu flow)
        if (health == null || p1 == null) TryFindRefs();

        if (health != null && healthSlider != null)
            healthSlider.value = health.Percent;          // 0..1, dead at 0

        if (p1 != null && reloadSlider != null)
            reloadSlider.value = p1.ReloadProgress.Value; // 1 = ready to fire

        if (p1 != null && ammoText != null)
            ammoText.text = $"{p1.AmmoInMag} / {p1.MagSize}";
    }

    /// <summary>Session services → FindObjectOfType, per the cross-tree lookup convention.</summary>
    private void TryFindRefs()
    {
        SplitCharacterManager mgr = FindObjectOfType<SplitCharacterManager>();
        if (mgr != null) health = mgr.GetComponent<Health>();   // Health sits on the character ROOT
        p1 = FindObjectOfType<PlayerOneController>();
    }
}