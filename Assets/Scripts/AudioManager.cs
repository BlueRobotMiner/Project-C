using UnityEngine;

/// <summary>
/// Scene audio service: named clip slots + a pool of one-shot sources.
/// World sounds are 3D at a position; UI sounds are 2D. NOT networked —
/// callers invoke it from code that already runs on every client (RPC handlers,
/// spawn callbacks), which is why replicated events double as audio events.
/// Put one in each gameplay scene + the Main Menu.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    public enum Sfx { Gunshot = 0, BulletImpact, Footstep, TongueShoot, TongueStick, TongueMiss, Spotted, UiClick, UiTick }

    [Header("Clips (drag in; one per slot)")]
    [SerializeField] private AudioClip gunshot;       // P1 + AI (same rifle family)
    [SerializeField] private AudioClip bulletImpact;
    [SerializeField] private AudioClip footstep;
    [SerializeField] private AudioClip tongueShoot;   // tongue rolls out
    [SerializeField] private AudioClip tongueStick;   // thwip on attach
    [SerializeField] private AudioClip tongueMiss;    // fast snap-back
    [SerializeField] private AudioClip spotted;       // AI notice bark (optional)
    [SerializeField] private AudioClip uiClick;
    [SerializeField] private AudioClip uiTick;

    [Header("Mix")]
    [SerializeField] private int poolSize = 16;       // simultaneous sounds
    [Range(0f, 1f)] [SerializeField] private float worldVolume = 0.8f;
    [Range(0f, 1f)] [SerializeField] private float uiVolume = 0.6f;
    [Tooltip("Max audible distance for world sounds.")]
    [SerializeField] private float worldMaxDist = 60f;

    private AudioSource[] pool;
    private int next;

    private void Awake()
    {
        Instance = this;

        // pool of one-shot sources — no per-sound Instantiate/GC churn at 600 RPM
        pool = new AudioSource[poolSize];
        for (int i = 0; i < poolSize; i++)
        {
            AudioSource src = new GameObject($"Sfx{i}").AddComponent<AudioSource>();
            src.transform.SetParent(transform);
            src.playOnAwake = false;
            src.maxDistance = worldMaxDist;
            pool[i] = src;
        }
    }

    private AudioClip Clip(Sfx id) => id switch
    {
        Sfx.Gunshot      => gunshot,
        Sfx.BulletImpact => bulletImpact,
        Sfx.Footstep     => footstep,
        Sfx.TongueShoot  => tongueShoot,
        Sfx.TongueStick  => tongueStick,
        Sfx.TongueMiss   => tongueMiss,
        Sfx.Spotted      => spotted,
        Sfx.UiClick      => uiClick,
        Sfx.UiTick       => uiTick,
        _ => null
    };

    /// <summary>3D one-shot at a world position — gunshots, impacts, tongue, AI bark.</summary>
    public static void PlayAt(Sfx id, Vector3 pos, float pitchJitter = 0.08f)
    {
        if (Instance == null) return;                 // no manager in this scene — stay silent
        AudioSource src = Instance.pool[Instance.next];
        Instance.next = (Instance.next + 1) % Instance.pool.Length;

        src.spatialBlend = 1f;                        // full 3D
        src.transform.position = pos;
        src.clip = Instance.Clip(id);
        src.volume = Instance.worldVolume;
        src.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);   // small jitter kills machine-gun sameness
        src.Play();
    }

    /// <summary>2D one-shot — UI clicks, slider ticks.</summary>
    public static void PlayUi(Sfx id)
    {
        if (Instance == null) return;
        AudioSource src = Instance.pool[Instance.next];
        Instance.next = (Instance.next + 1) % Instance.pool.Length;

        src.spatialBlend = 0f;
        src.clip = Instance.Clip(id);
        src.volume = Instance.uiVolume;
        src.pitch = 1f;
        src.Play();
    }
}