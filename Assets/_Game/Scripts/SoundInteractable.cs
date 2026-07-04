using UnityEngine;

/// <summary>
/// Интерактивный объект, который при взаимодействии просто проигрывает звук.
/// Подсвечивается так же, как предметы памяти и кровать.
/// </summary>
[RequireComponent(typeof(Collider))]
public class SoundInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private string prompt = "Послушать";

    [Header("Звук")]
    [Tooltip("Клип, который проигрывается при взаимодействии (через AudioManager).")]
    [SerializeField] private AudioClip clip;

    [Header("Подсветка")]
    [Tooltip("Если пусто — берётся первый Renderer в потомках. Материал должен быть URP/Lit.")]
    [SerializeField] private Renderer highlightRenderer;
    [SerializeField] private Color highlightColor = new Color(1f, 0.85f, 0.45f);
    [SerializeField] private float highlightIntensity = 1.6f;

    private Material highlightMat;

    public string Prompt => prompt;

    void Awake()
    {
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
        AudioManager.PlaySFX(clip);
    }
}
