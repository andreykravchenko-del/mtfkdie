using UnityEngine;

/// <summary>
/// Кровать: интерактивный объект, который "ложится" ГГ и запускает финал.
/// Подсвечивается так же, как предметы памяти.
/// </summary>
[RequireComponent(typeof(Collider))]
public class BedInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private string prompt = "Лечь";
    [Tooltip("Реплики ГГ, если лечь слишком рано (осмотрено меньше нужного числа значимых предметов).")]
    [TextArea(2, 3)] [SerializeField] private string[] tooEarlyLines =
        { "Ещё рано.", "Слишком многое не вспомнено." };
    [SerializeField] private Renderer highlightRenderer;
    [SerializeField] private Color highlightColor = new Color(0.55f, 0.65f, 1f);
    [SerializeField] private float highlightIntensity = 1.2f;

    private Material highlightMat;

    public string Prompt => prompt;

    void Awake()
    {
        if (highlightRenderer == null) highlightRenderer = GetComponentInChildren<Renderer>();
        if (highlightRenderer != null)
        {
            highlightMat = highlightRenderer.material;
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
        // Финал закрыт, пока не осмотрено достаточно значимых предметов.
        var gm = GameManager.Instance;
        if (gm != null && !gm.FinaleUnlocked)
        {
            if (NarrativeSystem.Instance != null && tooEarlyLines != null && tooEarlyLines.Length > 0)
                NarrativeSystem.Instance.Play(tooEarlyLines);
            return;
        }

        SetHighlight(false);
        if (EndingController.Instance != null) EndingController.Instance.TriggerEnding();
    }
}
