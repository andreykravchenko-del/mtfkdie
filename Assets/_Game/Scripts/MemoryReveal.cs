using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Полноэкранная картинка-воспоминание после успешного запоминания.
/// Любой клик / E / пробел / Enter — закрыть и вернуться к исследованию.
/// (Структура готова под замену Image на видео в будущем.)
/// </summary>
public class MemoryReveal : MonoBehaviour
{
    public static MemoryReveal Instance { get; private set; }

    [SerializeField] private GameObject panel;
    [SerializeField] private Image image;
    [SerializeField] private TMP_Text caption;

    private bool showing;

    void Awake()
    {
        Instance = this;
        if (panel != null) panel.SetActive(false);
    }

    public void Show(MemoryData data)
    {
        if (data == null || data.memoryImage == null)
        {
            GameManager.Instance.SetMode(GameMode.Explore);
            return;
        }

        showing = true;
        GameManager.Instance.SetMode(GameMode.Reveal);
        if (panel != null) panel.SetActive(true);
        if (image != null) { image.sprite = data.memoryImage; image.enabled = true; }
        if (caption != null) caption.text = data.displayName;

        AudioManager.PlaySFX(data.revealSound); // доп. звук воспоминания
    }

    void Update()
    {
        if (!showing || !AdvancePressed()) return;

        showing = false;
        if (panel != null) panel.SetActive(false);
        GameManager.Instance.SetMode(GameMode.Explore);
    }

    static bool AdvancePressed()
    {
        var kb = Keyboard.current;
        var m = Mouse.current;
        return (kb != null && (kb.eKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame))
            || (m != null && m.leftButton.wasPressedThisFrame);
    }
}
