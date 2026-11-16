using UnityEngine;
using UnityEngine.UI;

public class SensitivityTab : MonoBehaviour
{
    [SerializeField] private Slider sensSlider;
    [SerializeField] private SettingsManager settings;
    [SerializeField] private TMPro.TMP_Text valueLabel;

    void OnEnable()
    {
        sensSlider.minValue = 0.5f; sensSlider.maxValue = 5f;
        sensSlider.SetValueWithoutNotify(settings.MouseSensitivity);
        valueLabel.text = settings.MouseSensitivity.ToString("0.0");
    }

    public void OnSlider(float v)
    {
        v = Mathf.Round(v * 10f) / 10f;             // snap to 0.1 → stable visuals
        settings.MouseSensitivity = v;
        valueLabel.text = v.ToString("0.0");
        settings.Save();                            // applied live next frame by CameraDirector
    }
}