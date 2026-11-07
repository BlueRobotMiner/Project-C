using UnityEngine;
using UnityEngine.UI;

    public class VideoTab : MonoBehaviour
    {
        [SerializeField] private Toggle fullscreenToggle, vsyncToggle;
        [SerializeField] private Dropdown resolutionDropdown, qualityDropdown;
        [SerializeField] private SettingsManager settings;

    void OnEnable()
    {
        // Resolution dropdown
        resolutionDropdown.ClearOptions();
        var opts = new System.Collections.Generic.List<string>();
        foreach (var r in settings.Resolutions)
            opts.Add($"{r.width} x {r.height} @ {r.refreshRate}Hz");
        resolutionDropdown.AddOptions(opts);
        resolutionDropdown.SetValueWithoutNotify(settings.ResolutionIndex);

        // Quality dropdown
        qualityDropdown.ClearOptions();
        foreach (var name in QualitySettings.names)
            qualityDropdown.options.Add(new Dropdown.OptionData(name));
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