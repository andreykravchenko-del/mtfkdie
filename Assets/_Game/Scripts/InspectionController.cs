using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Экран осмотра 3D-предмета: чёрный фон, увеличенная копия предмета (её крутим ЛКМ),
/// описание и механика запоминания.
///
/// Управление:
///   • открытие  — кликом по предмету (см. PlayerInteractor);
///   • зажать E  — наполнять шкалу (запоминать). Появляются реплики ГГ;
///   • зажать F  — опустошать шкалу (отменять). Играет серый шум; на нуле — выход, предмет остаётся;
///   • Esc / ПКМ / крестик — выйти без запоминания, предмет остаётся в комнате;
///   • R — заново проиграть звук предмета (шорох/мяуканье и т.п.).
/// Шкала доведена до 100% (E) → успех: баллы + картинка-воспоминание, предмет "собран".
/// </summary>
public class InspectionController : MonoBehaviour
{
    public static InspectionController Instance { get; private set; }

    [Header("Сцена осмотра")]
    [SerializeField] private Camera inspectCamera;
    [SerializeField] private Transform stagePivot;
    [SerializeField] private string inspectLayerName = "InspectStage";
    [SerializeField] private float rotateSpeed = 0.25f;

    [Header("Скорость шкалы (доля в секунду при отмене F)")]
    [SerializeField] private float cancelSpeed = 0.6f;

    [Header("UI")]
    [SerializeField] private GameObject inspectPanel;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text holdHintText;
    [SerializeField] private TMP_Text commentText;
    [SerializeField] private GameObject captureBarRoot;
    [SerializeField] private Image captureBarFill;

    [Header("Звук")]
    [SerializeField] private AudioClip openClip;
    [SerializeField] private AudioClip successClip;
    [Tooltip("Зациклённый серый шум — играет, пока зажат F (отмена запоминания).")]
    [SerializeField] private AudioSource greyNoiseSource;

    private Interactable currentItem;
    private GameObject stageInstance;
    private int inspectLayer;
    private float progress;
    private int lastCommentIndex;
    private bool active;

    void Awake()
    {
        Instance = this;
        inspectLayer = LayerMask.NameToLayer(inspectLayerName);
        if (inspectLayer < 0) inspectLayer = 0;
        if (inspectCamera != null) inspectCamera.gameObject.SetActive(false);
        if (inspectPanel != null) inspectPanel.SetActive(false);
    }

    public void Open(Interactable item)
    {
        currentItem = item;
        progress = 0f;
        lastCommentIndex = -1;
        active = true;

        GameManager.Instance.SetMode(GameMode.Inspect);
        if (inspectCamera != null) inspectCamera.gameObject.SetActive(true);
        if (inspectPanel != null) inspectPanel.SetActive(true);

        stageInstance = item.CreateInspectCopy(stagePivot, inspectLayer);

        var data = item.Data;
        if (nameText != null) nameText.text = data != null ? data.displayName : item.name;
        if (descriptionText != null) descriptionText.text = data != null ? data.description : "";
        if (commentText != null) commentText.text = "";
        if (holdHintText != null) holdHintText.text = "E — запомнить   •   F — отменить   •   Esc — назад";
        SetBar(0f, false);

        AudioManager.PlaySFX(openClip);
        PlayItemSound();
    }

    void Update()
    {
        if (!active) return;

        Rotate();

        var kb = Keyboard.current;
        var mouse = Mouse.current;

        // Переиграть звук предмета.
        if (kb != null && kb.rKey.wasPressedThisFrame) PlayItemSound();

        // Выход без запоминания (предмет остаётся) — Esc / ПКМ.
        if ((kb != null && kb.escapeKey.wasPressedThisFrame) ||
            (mouse != null && mouse.rightButton.wasPressedThisFrame))
        {
            Exit();
            return;
        }

        bool holdE = kb != null && kb.eKey.isPressed;
        bool holdF = kb != null && kb.fKey.isPressed;

        SetGreyNoise(holdF);

        if (holdF)
        {
            // Отмена: шкала падает; на нуле — выходим (предмет остаётся).
            progress -= cancelSpeed * Time.deltaTime;
            SetBar(progress, progress > 0f);
            if (progress <= 0f) { Cancel(); return; }
        }
        else if (holdE)
        {
            progress += Time.deltaTime / Mathf.Max(0.1f, currentItem.Data.captureDuration);
            UpdateComments();
            SetBar(progress, true);
            if (progress >= 1f) { Success(); return; }
        }
    }

    void Rotate()
    {
        if (stagePivot == null || inspectCamera == null) return;
        var mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.isPressed) return;

        Vector2 d = mouse.delta.ReadValue();
        stagePivot.Rotate(inspectCamera.transform.up, -d.x * rotateSpeed, Space.World);
        stagePivot.Rotate(inspectCamera.transform.right, d.y * rotateSpeed, Space.World);
    }

    void UpdateComments()
    {
        var comments = currentItem.Data != null ? currentItem.Data.captureComments : null;
        if (comments == null || comments.Length == 0) return;

        int idx = Mathf.Clamp(Mathf.FloorToInt(progress * comments.Length), 0, comments.Length - 1);
        if (idx != lastCommentIndex)
        {
            lastCommentIndex = idx;
            if (commentText != null) commentText.text = comments[idx];
        }
    }

    void PlayItemSound()
    {
        if (currentItem != null && currentItem.Data != null)
            AudioManager.PlaySFX(currentItem.Data.itemSound);
    }

    void Success()
    {
        var data = currentItem.Data;
        if (data != null)
        {
            if (data.kind == MemoryKind.Good) GameManager.Instance.AddLife(data.points);
            else GameManager.Instance.AddDeath(data.points);
        }
        currentItem.MarkCollected();
        AudioManager.PlaySFX(successClip);

        Teardown();
        if (MemoryReveal.Instance != null && data != null)
            MemoryReveal.Instance.Show(data);
        else
            GameManager.Instance.SetMode(GameMode.Explore);
    }

    /// Отмена запоминания через F (шкала опустела). Предмет остаётся.
    void Cancel()
    {
        Teardown();
        GameManager.Instance.SetMode(GameMode.Explore);
    }

    /// Выход из осмотра без запоминания (Esc / ПКМ / крестик). Предмет остаётся.
    /// Публичный — можно повесить на кнопку-крестик в UI.
    public void Exit()
    {
        if (!active) return;
        Teardown();
        GameManager.Instance.SetMode(GameMode.Explore);
    }

    /// Скрыть UI/камеру осмотра и убрать копию предмета (без смены режима).
    void Teardown()
    {
        active = false;
        progress = 0f;
        SetGreyNoise(false);
        if (stageInstance != null) Destroy(stageInstance);
        stageInstance = null;
        if (inspectCamera != null) inspectCamera.gameObject.SetActive(false);
        if (inspectPanel != null) inspectPanel.SetActive(false);
    }

    void SetGreyNoise(bool on)
    {
        if (greyNoiseSource == null) return;
        if (on && !greyNoiseSource.isPlaying) greyNoiseSource.Play();
        else if (!on && greyNoiseSource.isPlaying) greyNoiseSource.Stop();
    }

    void SetBar(float value, bool visible)
    {
        if (captureBarRoot != null) captureBarRoot.SetActive(visible);
        if (captureBarFill != null) captureBarFill.fillAmount = Mathf.Clamp01(value);
    }
}
