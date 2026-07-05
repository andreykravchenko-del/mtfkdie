using UnityEngine;

/// <summary>
/// Финал. Считает разницу diff = надежда − безнадёжность и выбирает одну из четырёх концовок:
///   • секретная — если игрок ЗАБЫЛ все предметы (ни одного не запомнил);
///   • хорошая (жизнь) — diff ≥ goodThreshold;
///   • плохая (смерть) — diff ≤ badThreshold;
///   • нейтральная — между порогами.
/// Концовки — статичные панели (Image + текст); death-панель обычно чёрный фон.
/// </summary>
public class EndingController : MonoBehaviour
{
    public static EndingController Instance { get; private set; }

    [Header("Панели концовок (могут содержать несколько картинок)")]
    [SerializeField] private GameObject lifePanel;     // светлая, чистая комната / терапия, друзья
    [SerializeField] private GameObject neutralPanel;  // открытая/нейтральная
    [SerializeField] private GameObject deathPanel;    // темнота
    [SerializeField] private GameObject secretPanel;   // «всё забыто»

    [Header("Звук")]
    [SerializeField] private AudioClip lifeMusic;       // тёплая музыка хорошей концовки
    [SerializeField] private AudioClip neutralMusic;    // музыка нейтральной концовки
    [SerializeField] private AudioClip deathGreyNoise;  // серый шум плохой концовки (зациклится)
    [SerializeField] private AudioClip secretSound;     // звук секретной концовки

    [Header("Пороги (diff = надежда − безнадёжность)")]
    [Tooltip("diff ≥ этого значения → хорошая концовка. По документу 7 (рассчитано на 18 значимых предметов).")]
    [SerializeField] private int goodThreshold = 7;
    [Tooltip("diff ≤ этого значения → плохая концовка. По документу -7.")]
    [SerializeField] private int badThreshold = -7;

    void Awake()
    {
        Instance = this;
        if (lifePanel != null) lifePanel.SetActive(false);
        if (neutralPanel != null) neutralPanel.SetActive(false);
        if (deathPanel != null) deathPanel.SetActive(false);
        if (secretPanel != null) secretPanel.SetActive(false);
    }

    public void TriggerEnding()
    {
        var gm = GameManager.Instance;
        gm.SetMode(GameMode.Ending);

        // Секретная концовка перебивает пороги: игрок забыл каждый предмет и ни одного не запомнил.
        // Условие ForgottenCount > 0 отсекает ложное срабатывание при пустом прогрессе (0 == 0 == total).
        bool secret = gm.ForgottenCount > 0
                   && gm.RememberedCount == 0
                   && gm.ForgottenCount == gm.TotalMemoryItems;
        if (secret) { Play(secretPanel, secretSound); return; }

        int diff = gm.Hope - gm.Despair;
        if (diff >= goodThreshold)     Play(lifePanel, lifeMusic);
        else if (diff <= badThreshold) Play(deathPanel, deathGreyNoise);
        else                           Play(neutralPanel, neutralMusic);
    }

    void Play(GameObject panel, AudioClip clip)
    {
        if (panel != null) panel.SetActive(true);
        AudioManager.PlayMusic(clip); // null-safe: если clip пуст — просто остановит музыку
    }
}
