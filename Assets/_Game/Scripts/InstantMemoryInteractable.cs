using UnityEngine;

/// <summary>
/// Предмет памяти БЕЗ осмотра и шкалы: по клику сразу выдаёт результат.
/// Подсвечивается как обычный <see cref="Interactable"/>, но не открывает
/// <see cref="InspectionController"/> — сразу начисляет очки, регистрирует выбор
/// и (при запоминании) показывает картинку-воспоминание.
/// </summary>
[RequireComponent(typeof(Collider))]
public class InstantMemoryInteractable : MonoBehaviour, IInteractable
{
    [Header("Данные")]
    [SerializeField] private MemoryData data;

    [Tooltip("Что делать по клику: true — запомнить (очки + картинка-воспоминание), false — забыть (предмет исчезает, без картинки).")]
    [SerializeField] private bool remember = true;

    [Header("Подсветка")]
    [Tooltip("Если пусто — берётся первый Renderer в потомках. Материал должен быть URP/Lit.")]
    [SerializeField] private Renderer highlightRenderer;
    [SerializeField] private Color highlightColor = new Color(1f, 0.85f, 0.45f);
    [SerializeField] private float highlightIntensity = 1.6f;

    [Header("Звук")]
    [Tooltip("Звук нажатия. Пусто — берётся revealSound из MemoryData на показе картинки.")]
    [SerializeField] private AudioClip clickClip;

    private Material highlightMat;
    private Collider col;
    private bool collected;

    public string Prompt => data != null ? data.displayName : name;

    void Awake()
    {
        col = GetComponent<Collider>();
        if (highlightRenderer == null) highlightRenderer = GetComponentInChildren<Renderer>();
        if (highlightRenderer != null)
        {
            highlightMat = highlightRenderer.material; // инстанс — эмиссию можно менять
            highlightMat.EnableKeyword("_EMISSION");
            highlightMat.SetColor("_EmissionColor", Color.black);
        }
    }

    public void SetHighlight(bool on)
    {
        if (highlightMat == null) return;
        highlightMat.SetColor("_EmissionColor", on ? highlightColor * highlightIntensity : Color.black);
    }

    public void Interact(PlayerInteractor interactor)
    {
        // data == null не расходуем: иначе предмет крутит счётчики, не входя в TotalMemoryItems —
        // сломается условие секретной концовки (та же логика, что в Interactable).
        if (collected || data == null || GameManager.Instance == null) return;
        collected = true;

        GameManager.Instance.AddHope(data.HopeFor(remember));
        GameManager.Instance.AddDespair(data.DespairFor(remember));
        if (remember) GameManager.Instance.RegisterRemember(data.significant);
        else          GameManager.Instance.RegisterForget(data.significant);

        SetHighlight(false);
        if (col != null) col.enabled = false; // выпадает из рейкаста интерактора
        if (clickClip != null) AudioManager.PlaySFX(clickClip);

        if (remember && MemoryReveal.Instance != null)
        {
            MemoryReveal.Instance.Show(data); // картинка-воспоминание сразу
        }
        else if (!remember)
        {
            gameObject.SetActive(false); // забытый предмет исчезает из комнаты
        }
    }
}
