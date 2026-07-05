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

    [Tooltip("Отдельный источник для звука предмета — его можно остановить (StopItem), в отличие от one-shot SFX.")]
    [SerializeField] private AudioSource itemSource;

    [Tooltip("Управляемый канал: не наслаивает звук и умеет останавливаться (PlaySound/StopSound).")]
    [SerializeField] private AudioSource soundSource;

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
        if (itemSource == null)
        {
            itemSource = gameObject.AddComponent<AudioSource>();
            itemSource.playOnAwake = false;
        }
        if (soundSource == null)
        {
            soundSource = gameObject.AddComponent<AudioSource>();
            soundSource.playOnAwake = false;
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

    /// Звук предмета на отдельном источнике — играет, пока показывают картинку-воспоминание,
    /// и обрывается StopItem() при её закрытии. loop=true — тянется всё время показа.
    public static void PlayItem(AudioClip clip, bool loop = true)
    {
        if (Instance == null || Instance.itemSource == null) return;
        Instance.itemSource.Stop();
        if (clip == null) return;
        Instance.itemSource.clip = clip;
        Instance.itemSource.loop = loop;
        Instance.itemSource.Play();
    }

    /// Остановить звук предмета (при выходе из показа картинки).
    public static void StopItem()
    {
        if (Instance == null || Instance.itemSource == null) return;
        Instance.itemSource.Stop();
    }
    /// Играет ли сейчас управляемый звук (канал soundSource).
    public static bool IsSoundPlaying => Instance != null && Instance.soundSource != null && Instance.soundSource.isPlaying;

    /// Проиграть звук, но не перезапускать, пока предыдущий ещё играет.
    public static void PlaySound(AudioClip clip)
    {
        if (clip == null || Instance == null || Instance.soundSource == null) return;
        if (Instance.soundSource.isPlaying) return; // не наслаивать / не перезапускать
        Instance.soundSource.clip = clip;
        Instance.soundSource.Play();
    }

    /// Остановить управляемый звук.
    public static void StopSound()
    {
        if (Instance == null || Instance.soundSource == null) return;
        Instance.soundSource.Stop();
    }

    public static void PlayMusic(AudioClip clip, float volume = 1f)
    {
        if (Instance == null || Instance.musicSource == null) return;
        if (clip == null) { Instance.musicSource.Stop(); return; }
        Instance.musicSource.clip = clip;
        Instance.musicSource.volume = volume;
        Instance.musicSource.Play();
    }

    /// Вернуть штатную фоновую музыку (после меню/паузы) — переиспользует настроенные в сцене клип и громкость.
    public static void PlayBackground()
    {
        if (Instance == null) return;
        PlayMusic(Instance.backgroundMusic, Instance.musicVolume);
    }
}
