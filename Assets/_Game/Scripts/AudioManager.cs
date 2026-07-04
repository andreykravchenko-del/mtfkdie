using UnityEngine;

/// <summary>
/// Лёгкий доступ к звуку из любого скрипта: AudioManager.PlaySFX(clip) / PlayMusic(clip).
/// Держит два AudioSource — музыку (loop) и SFX (one-shot). Если их нет — создаёт сам.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;

    [Header("Фоновая музыка")]
    [Tooltip("Играет автоматически со старта сцены (зациклённо).")]
    [SerializeField] private AudioClip backgroundMusic;
    [SerializeField] [Range(0f, 1f)] private float musicVolume = 0.6f;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.playOnAwake = false;
        }
        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
        }
    }

    void Start()
    {
        if (backgroundMusic != null) PlayMusic(backgroundMusic, musicVolume);
    }

    public static void PlaySFX(AudioClip clip)
    {
        if (clip == null || Instance == null || Instance.sfxSource == null) return;
        Instance.sfxSource.PlayOneShot(clip);
    }

    public static void PlayMusic(AudioClip clip, float volume = 1f)
    {
        if (Instance == null || Instance.musicSource == null) return;
        if (clip == null) { Instance.musicSource.Stop(); return; }
        Instance.musicSource.clip = clip;
        Instance.musicSource.volume = volume;
        Instance.musicSource.Play();
    }
}
