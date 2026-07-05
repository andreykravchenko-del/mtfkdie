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
    [Tooltip("Звук при успешном забывании (F доведён до 100%).")]
    [SerializeField] private AudioClip forgetClip;
    [Tooltip("Зациклённый серый шум — играет, пока зажат F (забывание).")]
    [SerializeField] private AudioSource greyNoiseSource;

    [Header("Цвета шкалы (E — запомнить / F — забыть)")]
    [SerializeField] private Color rememberColor = new Color(0.5f, 0.85f, 0.6f);
    [SerializeField] private Color forgetColor   = new Color(0.6f, 0.6f, 0.62f);

    [Header("Глитч реплики при забывании")]
    [Tooltip("Материал-пресет с шейдером TextMeshPro/Mobile/Distance Field Glitch. Пусто — эффект выключен.")]
    [SerializeField] private Material commentGlitchMaterial;

    private Interactable currentItem;
    private GameObject stageInstance;
    private int inspectLayer;
    private float rememberProgress;
    private float forgetProgress;
    private int lastCommentIndex;
    private bool active;
    private Material commentGlitchInstance;
    private static readonly int GlitchAmountId = Shader.PropertyToID("_GlitchAmount");

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
        rememberProgress = 0f;
        forgetProgress = 0f;
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
        EnsureCommentGlitch();
        SetCommentGlitch(0f);
        if (holdHintText != null) holdHintText.text = "E — запомнить   •   F — забыть   •   Esc — назад";
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

        // Явные намерения через XOR: одновременный E+F ничего не заполняет.
        bool wantRemember = holdE && !holdF;
        bool wantForget   = holdF && !holdE;

        SetGreyNoise(wantForget);

        float dur = Mathf.Max(0.1f, currentItem.Data != null ? currentItem.Data.captureDuration : 3f);
        float decay = cancelSpeed * Time.deltaTime;

        if (wantRemember)
        {
            forgetProgress = Mathf.Max(0f, forgetProgress - decay);
            rememberProgress += Time.deltaTime / dur;
            UpdateComments(rememberProgress);
            SetCommentGlitch(0f);
            ShowBar(rememberProgress, rememberColor, 0 /*Left*/);
            if (rememberProgress >= 1f) { ResolveRemember(); return; }
        }
        else if (wantForget)
        {
            rememberProgress = Mathf.Max(0f, rememberProgress - decay);
            forgetProgress += Time.deltaTime / dur;
            // Затираем оставшуюся реплику глитчем по мере забывания.
            bool hasComment = commentText != null && !string.IsNullOrEmpty(commentText.text);
            SetCommentGlitch(hasComment ? forgetProgress : 0f);
            ShowBar(forgetProgress, forgetColor, 1 /*Right*/);
            if (forgetProgress >= 1f) { ResolveForget(); return; }
        }
        else
        {
            // Простой или E+F вместе — обе шкалы угасают, показываем большую.
            rememberProgress = Mathf.Max(0f, rememberProgress - decay);
            forgetProgress   = Mathf.Max(0f, forgetProgress   - decay);
            SetCommentGlitch(0f);
            float shown = Mathf.Max(rememberProgress, forgetProgress);
            SetBar(shown, shown > 0f);
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

    void UpdateComments(float progress)
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

    /// Успешное запоминание (E доведён до 100%): очки за «Запомнить» + картинка-воспоминание.
    void ResolveRemember()
    {
        var data = currentItem.Data;
        if (data != null)
        {
            GameManager.Instance.AddHope(data.HopeFor(true));
            GameManager.Instance.AddDespair(data.DespairFor(true));
        }
        GameManager.Instance.RegisterRemember(data != null && data.significant);
        currentItem.MarkCollected();
        AudioManager.PlaySFX(successClip);

        Teardown();
        if (MemoryReveal.Instance != null && data != null)
            MemoryReveal.Instance.Show(data);
        else
            GameManager.Instance.SetMode(GameMode.Explore);
    }

    /// Забывание (F доведён до 100%): очки за «Забыть». Предмет расходуется, но БЕЗ картинки-воспоминания.
    void ResolveForget()
    {
        var data = currentItem.Data;
        if (data != null)
        {
            GameManager.Instance.AddHope(data.HopeFor(false));
            GameManager.Instance.AddDespair(data.DespairFor(false));
        }
        GameManager.Instance.RegisterForget(data != null && data.significant);
        currentItem.MarkCollected();
        AudioManager.PlaySFX(forgetClip);

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
        rememberProgress = 0f;
        forgetProgress = 0f;
        SetGreyNoise(false);
        SetCommentGlitch(0f);
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

    /// Показать активную шкалу заданным цветом и направлением заливки (0=Left / 1=Right).
    void ShowBar(float value, Color color, int fillOrigin)
    {
        if (captureBarRoot != null) captureBarRoot.SetActive(true);
        if (captureBarFill != null)
        {
            captureBarFill.fillOrigin = fillOrigin;
            captureBarFill.color = color;
            captureBarFill.fillAmount = Mathf.Clamp01(value);
        }
    }

    /// Переключает реплику на инстанс glitch-материала (создаётся один раз).
    void EnsureCommentGlitch()
    {
        if (commentGlitchMaterial == null || commentText == null) return;
        if (commentGlitchInstance == null) commentGlitchInstance = new Material(commentGlitchMaterial);
        commentText.fontSharedMaterial = commentGlitchInstance;
    }

    /// Сила затирания реплики (0 = чёткий текст, 1 = максимальные помехи).
    /// TMP для fallback-глифов (заглавная кириллица, multi-atlas) сам создаёт материалы,
    /// наследующие glitch-шейдер от основного и со СВОИМ правильным атласом, но НЕ
    /// синхронизирует _GlitchAmount. Поэтому ставим его на основном инстансе И на всех
    /// суб-меш-материалах — тогда глитчится и латиница, и кириллица.
    void SetCommentGlitch(float amount)
    {
        amount = Mathf.Clamp01(amount);
        if (commentGlitchInstance != null) commentGlitchInstance.SetFloat(GlitchAmountId, amount);
        if (commentText == null) return;
        var subs = commentText.GetComponentsInChildren<TMP_SubMeshUI>(true);
        for (int i = 0; i < subs.Length; i++)
        {
            var m = subs[i].sharedMaterial;
            if (m != null && m.HasProperty(GlitchAmountId)) m.SetFloat(GlitchAmountId, amount);
        }
    }
}
