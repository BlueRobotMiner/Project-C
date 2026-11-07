using UnityEngine;

public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance { get; private set; }

    [Header("Video")]
    public bool Fullscreen;
    public int  ResolutionIndex;
    public int  QualityIndex;
    public bool VSync;

    [Header("Audio")] public float Master = 1f, Music = 1f, Sfx = 1f;
    [Header("Controls")] public float MouseSensitivity = 2f;

    public Resolution[] Resolutions { get; private set; }

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);            // settings survive scene loads
        Resolutions = Screen.resolutions;
        Load();
    }

    public void Save()
    {
        PlayerPrefs.SetInt("fullscreen", Fullscreen ? 1 : 0);
        PlayerPrefs.SetInt("resIndex", ResolutionIndex);
        PlayerPrefs.SetInt("quality", QualityIndex);
        PlayerPrefs.SetInt("vsync", VSync ? 1 : 0);
        PlayerPrefs.SetFloat("master", Master);
        PlayerPrefs.SetFloat("music", Music);
        PlayerPrefs.SetFloat("sfx", Sfx);
        PlayerPrefs.SetFloat("sens", MouseSensitivity);
        PlayerPrefs.Save();
    }

    void Load()
    {
        Fullscreen       = PlayerPrefs.GetInt("fullscreen", 1) == 1;
        ResolutionIndex  = PlayerPrefs.GetInt("resIndex", Mathf.Max(0, Resolutions.Length - 1));
        QualityIndex     = PlayerPrefs.GetInt("quality", QualitySettings.GetQualityLevel());
        VSync            = PlayerPrefs.GetInt("vsync", 1) == 1;
        Master           = PlayerPrefs.GetFloat("master", 1f);
        Music            = PlayerPrefs.GetFloat("music", 1f);
        Sfx              = PlayerPrefs.GetFloat("sfx", 1f);
        MouseSensitivity = PlayerPrefs.GetFloat("sens", 2f);
        Apply();
    }

    public void Apply()
    {
        QualitySettings.SetQualityLevel(QualityIndex);
        var r = Resolutions[ResolutionIndex];
        Screen.SetResolution(r.width, r.height, Fullscreen);
        QualitySettings.vSyncCount = VSync ? 1 : 0;
        AudioListener.volume = Master;             // upgrade to AudioMixer later
        // MouseSensitivity is READ by CameraDirector — nothing to apply here
    }
}