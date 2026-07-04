using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Прицел в центре + подсказка "ЛКМ — [название]".
/// Точка меняет цвет, когда под прицелом есть интерактивный объект.
/// </summary>
public class ReticleUI : MonoBehaviour
{
    [SerializeField] private Graphic dot;
    [SerializeField] private Color idleColor = new Color(1f, 1f, 1f, 0.45f);
    [SerializeField] private Color activeColor = new Color(1f, 0.85f, 0.45f, 1f);
    [SerializeField] private TMP_Text promptText;

    void Awake() => Set(false, null);

    void Start()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ModeChanged += HandleMode;
            HandleMode(GameManager.Instance.Mode);
        }
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null) GameManager.Instance.ModeChanged -= HandleMode;
    }

    // Прицел виден только в режиме исследования.
    void HandleMode(GameMode mode)
    {
        if (dot != null) dot.enabled = mode == GameMode.Explore;
        if (mode != GameMode.Explore && promptText != null) promptText.gameObject.SetActive(false);
    }

    public void Set(bool active, string prompt)
    {
        if (dot != null) dot.color = active ? activeColor : idleColor;

        if (promptText != null)
        {
            bool show = active && !string.IsNullOrEmpty(prompt);
            promptText.gameObject.SetActive(show);
            if (show) promptText.text = $"ЛКМ — {prompt}";
        }
    }
}
