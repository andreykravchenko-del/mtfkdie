using UnityEngine;

/// <summary>
/// Осколок памяти в комнате. Наведение подсвечивает его (эмиссия), E открывает осмотр.
/// После успешного запоминания помечается Collected и больше не берётся в рейкаст.
/// </summary>
[RequireComponent(typeof(Collider))]
public class Interactable : MonoBehaviour, IInteractable
{
    [Header("Данные")]
    [SerializeField] private MemoryData data;

    [Header("Подсветка")]
    [Tooltip("Если пусто — берётся первый Renderer в потомках. Материал должен быть URP/Lit.")]
    [SerializeField] private Renderer highlightRenderer;
    [SerializeField] private Color highlightColor = new Color(1f, 0.85f, 0.45f);
    [SerializeField] private float highlightIntensity = 1.6f;

    [Header("Осмотр (3D)")]
    [Tooltip("Необязательно: отдельная модель для экрана осмотра. Пусто — клонируется сам объект.")]
    [SerializeField] private GameObject inspectModelOverride;
    [SerializeField] private Vector3 inspectLocalScale = Vector3.one;
    [SerializeField] private Vector3 inspectLocalEuler = Vector3.zero;

    private Material highlightMat;
    private Collider col;

    public MemoryData Data => data;
    public bool Collected { get; private set; }
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
        if (Collected || InspectionController.Instance == null) return;
        InspectionController.Instance.Open(this);
    }

    /// Предмет запомнен: остаётся в комнате, но больше не осматривается.
    public void MarkCollected()
    {
        Collected = true;
        SetHighlight(false);
        if (col != null) col.enabled = false; // выпадает из рейкаста интерактора
    }

    /// Создаёт визуальную копию для сцены осмотра: без коллайдеров/скриптов, на нужном слое.
    public GameObject CreateInspectCopy(Transform parent, int layer)
    {
        GameObject source = inspectModelOverride != null ? inspectModelOverride : gameObject;
        GameObject copy = Instantiate(source, parent);

        copy.transform.localPosition = Vector3.zero;
        copy.transform.localRotation = Quaternion.Euler(inspectLocalEuler);
        copy.transform.localScale = inspectLocalScale;

        // Снимаем скрипты (в т.ч. сам Interactable), но коллайдеры НЕ трогаем: их нельзя
        // удалить, пока на копии висит Interactable (RequireComponent), а вреда они не несут —
        // копия стоит далеко на слое InspectStage и в рейкаст игрока не попадает.
        foreach (var b in copy.GetComponentsInChildren<MonoBehaviour>()) Destroy(b);
        SetLayerRecursive(copy, layer);
        return copy;
    }

    static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform) SetLayerRecursive(child.gameObject, layer);
    }
}
