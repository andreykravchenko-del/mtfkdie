using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Рейкаст из центра камеры в режиме Explore. Подсвечивает объект под прицелом,
/// показывает подсказку и по нажатию E запускает его Interact().
/// </summary>
public class PlayerInteractor : MonoBehaviour
{
    [SerializeField] private Camera cam;
    [SerializeField] private float range = 2.5f;
    [SerializeField] private LayerMask interactMask = ~0;
    [SerializeField] private ReticleUI reticle;

    private IInteractable current;

    void Awake()
    {
        if (cam == null) cam = GetComponentInChildren<Camera>();
        if (cam == null) cam = Camera.main;
    }

    void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.Mode != GameMode.Explore)
        {
            if (current != null) SetCurrent(null);
            return;
        }

        IInteractable found = null;
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (Physics.Raycast(ray, out RaycastHit hit, range, interactMask, QueryTriggerInteraction.Ignore))
            found = hit.collider.GetComponentInParent<IInteractable>();

        SetCurrent(found);

        // Открываем предмет / ложимся в кровать кликом (ЛКМ).
        if (current != null && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            current.Interact(this);
    }

    void SetCurrent(IInteractable it)
    {
        if (ReferenceEquals(current, it)) return;
        current?.SetHighlight(false);
        current = it;
        current?.SetHighlight(true);
        if (reticle != null) reticle.Set(current != null, current?.Prompt);
    }
}
