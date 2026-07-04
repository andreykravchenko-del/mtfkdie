using UnityEngine;

public enum GameMode { Intro, Explore, Inspect, Narrative, Reveal, Ending }

/// <summary>
/// Единый источник правды о текущем режиме игры и счёте.
/// Движение/интеракт активны только в Explore; курсор блокируется только в Explore.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Копилки")]
    [SerializeField] private int lifePoints;
    [SerializeField] private int deathPoints;

    public int LifePoints => lifePoints;
    public int DeathPoints => deathPoints;

    public GameMode Mode { get; private set; } = GameMode.Explore;

    /// Оповещение о смене режима — можно подписаться из UI/звука.
    public event System.Action<GameMode> ModeChanged;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        // Если в сцене есть интро — оно само выставит режим Intro. Иначе стартуем в Explore.
        if (FindFirstObjectByType<IntroSequence>() == null)
            SetMode(GameMode.Explore);
    }

    public void SetMode(GameMode mode)
    {
        Mode = mode;
        // Курсор виден только в "меню-подобных" режимах; в Intro/Explore он скрыт и залочен.
        bool showCursor = mode == GameMode.Inspect || mode == GameMode.Narrative
                       || mode == GameMode.Reveal  || mode == GameMode.Ending;
        Cursor.lockState = showCursor ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = showCursor;
        ModeChanged?.Invoke(mode);
    }

    public void AddLife(int amount) => lifePoints += Mathf.Max(0, amount);
    public void AddDeath(int amount) => deathPoints += Mathf.Max(0, amount);
}
