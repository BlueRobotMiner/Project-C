using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Video settings tab: resolution / quality dropdowns (TextMeshPro — the type
/// Unity creates by default, so drag them straight in) and fullscreen / vsync
/// toggles (legacy uGUI Toggle; TMP has no toggle equivalent).
/// Every change applies + saves immediately through SettingsManager.
public class VideoTab : MonoBehaviour
{
    [SerializeField] private Toggle fullscreenToggle, vsyncToggle;

    // TMP_Dropdown, NOT UnityEngine.UI.Dropdown: the scene's dropdowns are
    // TextMeshPro objects and Unity will not accept them on a legacy field.
    [SerializeField] private TMP_Dropdown resolutionDropdown, qualityDropdown;

    [SerializeField] private SettingsManager settings;

    void OnEnable()
    {
        // Resolution dropdown — rebuilt from SettingsManager's cached list
        resolutionDropdown.ClearOptions();
        var opts = new List<string>();
        foreach (var r in settings.Resolutions)
            opts.Add($"{r.width} x {r.height} @ {r.refreshRate}Hz");
        resolutionDropdown.AddOptions(opts);
        resolutionDropdown.SetValueWithoutNotify(settings.ResolutionIndex);   // no OnValueChanged storm

        // Quality dropdown — straight from Unity's quality levels
        qualityDropdown.ClearOptions();
        qualityDropdown.AddOptions(new List<string>(QualitySettings.names));
        qualityDropdown.SetValueWithoutNotify(settings.QualityIndex);

        // Toggles — SetIsOnWithoutNotify so they don't fire OnValueChanged while refreshing
        fullscreenToggle.SetIsOnWithoutNotify(settings.Fullscreen);
        vsyncToggle.SetIsOnWithoutNotify(settings.VSync);
    }

    public void OnFullscreen(bool v)   { settings.Fullscreen = v;      settings.Apply(); settings.Save(); }
    public void OnVSync(bool v)        { settings.VSync = v;           settings.Apply(); settings.Save(); }
    public void OnResolution(int i)    { settings.ResolutionIndex = i; settings.Apply(); settings.Save(); }
    public void OnQuality(int i)       { settings.QualityIndex = i;    settings.Apply(); settings.Save(); }
}