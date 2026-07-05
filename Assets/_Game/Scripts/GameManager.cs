using UnityEngine;
using UnityEngine.Serialization;

public enum GameMode { Intro, Explore, Inspect, Narrative, Reveal, Ending }

/// <summary>
/// Единый источник правды о текущем режиме игры и счёте.
/// Движение/интеракт активны только в Explore; курсор блокируется только в Explore.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Копилки")]
    [FormerlySerializedAs("lifePoints")]  [SerializeField] private int hope;
    [FormerlySerializedAs("deathPoints")] [SerializeField] private int despair;

    public int Hope => hope;
    public int Despair => despair;

    [Header("Секретная концовка / открытие финала")]
    [Tooltip("Общее число ЗНАЧИМЫХ предметов памяти. -1 = посчитать автоматически по сцене на старте.")]
    [SerializeField] private int totalMemoryItemsOverride = -1;
    [Tooltip("Сколько значимых предметов нужно осмотреть (запомнить или забыть), чтобы кровать открыла финал.")]
    [SerializeField] private int itemsToOpenFinale = 12;

    /// Сколько ЗНАЧИМЫХ предметов игрок запомнил (E) и забыл (F) — для секретной концовки «всё забыть».
    public int RememberedCount { get; private set; }
    public int ForgottenCount { get; private set; }
    public int TotalMemoryItems { get; private set; }

    /// Сколько значимых предметов осмотрено всего (запомнено + забыто) — для открытия финала.
    public int SignificantInspected => RememberedCount + ForgottenCount;
    /// Открыт ли финал: осмотрено достаточно значимых предметов.
    public bool FinaleUnlocked => SignificantInspected >= itemsToOpenFinale;

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
        // Кешируем общее число предметов для секретной концовки: все Interactable существуют
        // на старте (собранные лишь помечаются Collected, из сцены не удаляются).
        TotalMemoryItems = totalMemoryItemsOverride >= 0
            ? totalMemoryItemsOverride
            : CountMemoryItems();

        // Если в сцене есть интро — оно само выставит режим Intro. Иначе стартуем в Explore.
        if (FindFirstObjectByType<IntroSequence>() == null)
            SetMode(GameMode.Explore);
    }

    static int CountMemoryItems()
    {
        int n = 0;
        foreach (var it in FindObjectsByType<Interactable>(FindObjectsSortMode.None))
            if (it.Data != null && it.Data.significant) n++;
        return n;
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

    public void AddHope(int amount)    => hope    += Mathf.Max(0, amount);
    public void AddDespair(int amount) => despair += Mathf.Max(0, amount);

    /// Учёт выбора игрока (для секретной концовки «всё забыть» и открытия финала).
    /// Простые (незначимые) предметы в счёт не идут.
    public void RegisterRemember(bool significant) { if (significant) RememberedCount++; }
    public void RegisterForget(bool significant)   { if (significant) ForgottenCount++; }
}
