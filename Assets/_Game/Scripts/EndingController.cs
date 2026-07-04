using UnityEngine;

/// <summary>
/// Финал. Сравнивает копилки: жизни > смерти → концовка "жизнь" (терапия, друзья);
/// иначе → концовка "смерть" (экран гаснет в чёрный, пустота).
/// Концовки — статичные панели (Image + текст); death-панель обычно чёрный фон.
/// </summary>
public class EndingController : MonoBehaviour
{
    public static EndingController Instance { get; private set; }

    [Header("Панели концовок (могут содержать несколько картинок)")]
    [SerializeField] private GameObject lifePanel;   // светлая, чистая комната / терапия, друзья
    [SerializeField] private GameObject deathPanel;  // темнота

    [Header("Звук")]
    [SerializeField] private AudioClip lifeMusic;       // тёплая музыка хорошей концовки
    [SerializeField] private AudioClip deathGreyNoise;  // серый шум плохой концовки (зациклится)

    void Awake()
    {
        Instance = this;
        if (lifePanel != null) lifePanel.SetActive(false);
        if (deathPanel != null) deathPanel.SetActive(false);
    }

    public void TriggerEnding()
    {
        GameManager.Instance.SetMode(GameMode.Ending);

        bool life = GameManager.Instance.LifePoints > GameManager.Instance.DeathPoints;
        if (life)
        {
            if (lifePanel != null) lifePanel.SetActive(true);
            AudioManager.PlayMusic(lifeMusic);
        }
        else
        {
            if (deathPanel != null) deathPanel.SetActive(true);
            AudioManager.PlayMusic(deathGreyNoise); // серый шум лупом (или тишина, если пусто)
        }
    }
}
