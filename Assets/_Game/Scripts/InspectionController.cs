using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using TMPro;

/// <summary>
/// Экран осмотра 3D-предмета: чёрный фон, увеличенная копия предмета (её крутим ЛКМ),
/// описание и механика запоминания.
///
/// Управление:
///   • открытие  — кликом по предмету (см. PlayerInteractor);
///   • зажать E  — наполнять шкалу (запоминать). Появляются реплики ГГ;
///   • зажать F  — опустошать шкалу (отменять). Играет серый шум; на нуле — выход, предмет остаётся;
///   • Esc / ПКМ / крестик — выйти без запоминания, предмет остаётся в комнате.
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

    [Header("Фон осмотра — заблюренная комната")]
    [Tooltip("Во сколько раз уменьшать снимок комнаты. Больше — сильнее размытие и дешевле.")]
    [SerializeField] private int blurDownsample = 8;
    [Tooltip("Затемнение фона для читаемости текста (0 — без затемнения).")]
    [SerializeField] [Range(0f, 1f)] private float backdropDim = 0.4f;

    private Interactable currentItem;
    private GameObject stageInstance;
    private int inspectLayer;
    private float rememberProgress;
    private float forgetProgress;
    private int lastCommentIndex;
    private bool active;
    private Material commentGlitchInstance;
    private static readonly int GlitchAmountId = Shader.PropertyToID("_GlitchAmount");
    private static readonly string[] GlitchProps = { "_GlitchShift", "_GlitchJitter", "_GlitchBandHeight", "_GlitchSpeed", "_GlitchDropout", "_GlitchDissolve", "_GlitchColor" };

    private Camera mainCam;
    private RenderTexture blurRT;
    private GameObject backdropQuad;
    private Mesh backdropMesh;
    private Material backdropMat;

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
        if (inspectCamera != null)
        {
            inspectCamera.gameObject.SetActive(true);
            // Камера осмотра — физическая (сенсор 36×24), из-за чего кадр рендерился квадратом с чёрными
            // полями. На время осмотра делаем её обычной с аспектом экрана — кадр заполняет весь экран.
            inspectCamera.usePhysicalProperties = false;
            inspectCamera.aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
        }
        if (inspectPanel != null) inspectPanel.SetActive(true);

        ShowBlurBackdrop(); // заблюренная комната вместо чёрного фона

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
        // Звук самого предмета во время осмотра НЕ играем — он звучит на показе картинки-воспоминания
        // (см. MemoryReveal) и обрывается при её закрытии.
    }

    void Update()
    {
        if (!active) return;

        Rotate();

        var kb = Keyboard.current;
        var mouse = Mouse.current;

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

        // Во время заполнения шкалы забывания — тишина. Звук забытия играем один раз по завершении
        // (в ResolveForget), а не зациклённо во время удержания F.

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

        Vector2 d = InputUtils.MouseLookDelta();
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
        currentItem.MarkForgotten(); // забытый предмет выключается (исчезает из комнаты)
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
        if (backdropQuad != null) backdropQuad.SetActive(false);
        if (inspectCamera != null) inspectCamera.gameObject.SetActive(false);
        if (inspectPanel != null) inspectPanel.SetActive(false);
    }

    void SetGreyNoise(bool on)
    {
        if (greyNoiseSource == null) return;
        if (on && !greyNoiseSource.isPlaying) greyNoiseSource.Play();
        else if (!on && greyNoiseSource.isPlaying) greyNoiseSource.Stop();
    }

    // ---------- Заблюренная комната как фон осмотра ----------
    // Снимаем кадр главной камеры (комнату) в маленький RT, слегка сглаживаем и рисуем как задник
    // на слое осмотра ЗА предметом. Камера осмотра рисует и задник, и предмет — предмет остаётся резким.
    void ShowBlurBackdrop()
    {
        if (inspectCamera == null) return;
        EnsureBackdrop();
        bool ok = CaptureRoomBlur();
        if (!ok) return; // нет главной камеры/шейдера — остаётся штатный чёрный фон
        PlaceBackdrop();
        if (backdropQuad != null) backdropQuad.SetActive(true);
    }

    void EnsureBackdrop()
    {
        if (backdropQuad != null) return;

        // Собственный меш (а не примитив-Quad, привязанный к камере): вершины будем ставить прямо в
        // мировые углы кадра камеры — тогда размер/аспект/физкамера учтены автоматически.
        backdropQuad = new GameObject("InspectBlurBackdrop", typeof(MeshFilter), typeof(MeshRenderer));
        // Служебный рантайм-объект: прячем из иерархии и не сохраняем — чтобы его нельзя было выбрать
        // в Инспекторе (иначе при его уничтожении редактор сыплет NullReference в GameObjectInspector).
        backdropQuad.hideFlags = HideFlags.HideAndDontSave;
        backdropQuad.layer = inspectLayer;
        backdropQuad.transform.SetParent(null, false);
        backdropQuad.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        backdropQuad.transform.localScale = Vector3.one; // мировые координаты 1:1

        backdropMesh = new Mesh { name = "InspectBackdropMesh" };
        backdropMesh.MarkDynamic();
        backdropQuad.GetComponent<MeshFilter>().sharedMesh = backdropMesh;

        backdropMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        if (backdropMat.HasProperty("_Cull")) backdropMat.SetFloat("_Cull", 0f); // двусторонний
        var mr = backdropQuad.GetComponent<MeshRenderer>();
        mr.sharedMaterial = backdropMat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        backdropQuad.SetActive(false);
    }

    void PlaceBackdrop()
    {
        if (backdropQuad == null || backdropMesh == null) return;

        // Гарантируем широкий кадр (см. Open): обычная камера + аспект экрана, иначе углы вьюпорта
        // считаются от квадратной физ-проекции и задник выходит квадратом.
        inspectCamera.usePhysicalProperties = false;
        inspectCamera.aspect = (float)Screen.width / Mathf.Max(1, Screen.height);

        // Дальше предмета, но в пределах far-plane — предмет всегда перед фоном.
        float stageDist = stagePivot != null
            ? Vector3.Distance(inspectCamera.transform.position, stagePivot.position)
            : 10f;
        float d = Mathf.Clamp(stageDist * 3f + 5f, 10f, inspectCamera.farClipPlane * 0.9f);

        // Вершины = точные мировые углы кадра на расстоянии d.
        Vector3 bl = inspectCamera.ViewportToWorldPoint(new Vector3(0f, 0f, d));
        Vector3 br = inspectCamera.ViewportToWorldPoint(new Vector3(1f, 0f, d));
        Vector3 tl = inspectCamera.ViewportToWorldPoint(new Vector3(0f, 1f, d));
        Vector3 tr = inspectCamera.ViewportToWorldPoint(new Vector3(1f, 1f, d));

        // Небольшой запас наружу от центра — чтобы точно не было каймы по краю.
        Vector3 c = (bl + br + tl + tr) * 0.25f;
        const float m = 1.1f;
        bl = c + (bl - c) * m; br = c + (br - c) * m;
        tl = c + (tl - c) * m; tr = c + (tr - c) * m;

        backdropMesh.Clear();
        backdropMesh.vertices  = new[] { bl, br, tl, tr };
        backdropMesh.uv        = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
        backdropMesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        backdropMesh.RecalculateBounds();
    }

    bool CaptureRoomBlur()
    {
        if (mainCam == null) mainCam = Camera.main;
        if (mainCam == null || backdropMat == null) return false;

        int w = Mathf.Max(16, Screen.width  / Mathf.Max(1, blurDownsample));
        int h = Mathf.Max(16, Screen.height / Mathf.Max(1, blurDownsample));
        if (blurRT == null || blurRT.width != w || blurRT.height != h)
        {
            if (blurRT != null) blurRT.Release();
            blurRT = new RenderTexture(w, h, 24) { filterMode = FilterMode.Bilinear };
            blurRT.Create();
        }

        // Рендерим комнату в маленький RT (URP-совместимо, синхронно). Низкое разрешение + билинейный
        // апскейл на задник = мягкое размытие. Без Graphics.Blit — в URP он рисует источник не на весь
        // destination (центр/угол), из-за чего и был «маленький квадрат с чёрным вокруг».
        var req = new RenderPipeline.StandardRequest { destination = blurRT };
        if (RenderPipeline.SupportsRenderRequest(mainCam, req))
            mainCam.SubmitRenderRequest(req);

        backdropMat.SetTexture("_BaseMap", blurRT);
        if (backdropMat.HasProperty("_BaseColor"))
        {
            float k = 1f - Mathf.Clamp01(backdropDim);
            backdropMat.SetColor("_BaseColor", new Color(k, k, k, 1f));
        }
        return true;
    }

    void OnDestroy()
    {
        if (blurRT != null) { blurRT.Release(); blurRT = null; }
        if (backdropQuad != null) Destroy(backdropQuad); // самостоятельный объект, не потомок камеры
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
        var glitchShader = commentGlitchMaterial.shader;
        var fontMat = commentText.fontSharedMaterial;

        // Исходный (не-glitch) материал ТЕКУЩЕГО шрифта — источник правильного атласа.
        // Если на тексте уже наш инстанс — берём его же (атлас не менялся).
        Material src = (fontMat != null && fontMat.shader == glitchShader) ? commentGlitchInstance : fontMat;
        if (src == null) return;
        var atlas = src.GetTexture("_MainTex");

        // (Пере)создаём glitch-инстанс, если его нет или сменился шрифт (другой атлас).
        // Берём атлас/свойства из материала шрифта, а шейдер и glitch-параметры — из пресета.
        if (commentGlitchInstance == null || commentGlitchInstance.GetTexture("_MainTex") != atlas)
        {
            if (commentGlitchInstance != null) Destroy(commentGlitchInstance);
            commentGlitchInstance = new Material(src);
            commentGlitchInstance.shader = glitchShader;
            for (int p = 0; p < GlitchProps.Length; p++)
                if (commentGlitchMaterial.HasProperty(GlitchProps[p]))
                    commentGlitchInstance.SetFloat(GlitchProps[p], commentGlitchMaterial.GetFloat(GlitchProps[p]));
        }
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
