using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using TMPro;

/// <summary>
/// Главное меню + пауза, построенные кодом поверх игровой сцены.
/// Фон меню — сама квартира (живая игровая сцена за полупрозрачной панелью): ничего не рендерим
/// заново, а нагнетаем настроение поверх — холодная тёмная палитра, медленное проявление из
/// черноты, тяжёлый дрейф камеры, усиленная постобработка (десатурация + виньетка через рантайм
/// URP Volume) и давящий пульс сердца (звук безнадёжной концовки) вместо обычной музыки.
///   • «Играть» — снимает мрачный грейд, возвращает обычную музыку и запускает вступление;
///   • Esc в игре (Explore) — пауза; Esc в паузе — продолжить;
///   • «В главное меню» — перезагружает сцену (полный сброс прогресса → снова это меню).
/// Весь UI создаётся в рантайме, поэтому в сцене достаточно одного этого компонента.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("Заголовок")]
    [Tooltip("Заголовок в меню. Пусто → название продукта из Player Settings.")]
    [SerializeField] private string gameTitle = "";
    [Tooltip("Тусклая строка-эпиграф под заголовком. Пусто → без подписи.")]
    [SerializeField] private string tagline = "всё, что осталось — эта комната.";

    [Header("Настроение")]
    [Tooltip("Секунд на проявление из черноты при входе в меню.")]
    [SerializeField] private float fadeInDuration = 2.5f;
    [Tooltip("Усиливать постобработку (десатурация + виньетка) живой квартиры под меню.")]
    [SerializeField] private bool menuColorGrade = true;

    [Header("Дрейф камеры на фоне")]
    [SerializeField] private bool idleCameraSway = true;
    [SerializeField] private float swayAmplitude = 3.5f;
    [SerializeField] private float swaySpeed = 0.12f;
    [Tooltip("Постоянный наклон взгляда вниз, градусы (уныние).")]
    [SerializeField] private float pitchDownBias = 2f;

    [Header("Звук")]
    [Tooltip("Зациклённый давящий фон меню (обычно пульс сердца плохой концовки).")]
    [SerializeField] private AudioClip menuDrone;
    [SerializeField] [Range(0f, 1f)] private float menuDroneVolume = 0.35f;
    [Tooltip("Тихий клик по кнопкам меню.")]
    [SerializeField] private AudioClip clickSound;

    private IntroSequence intro;
    private GameObject mainPanel;
    private GameObject pausePanel;
    private Image fadeOverlay;
    private GameObject moodVolumeGO;

    private Transform swayCam;
    private Quaternion swayCamBaseRot;
    private bool swayCaptured;
    private float swayTime;

    void Awake()
    {
        // Забираем управление стартом у интро: облёт запустится только по кнопке «Играть».
        intro = FindFirstObjectByType<IntroSequence>();
        if (intro != null) intro.DisableAutoPlay();
    }

    void Start()
    {
        BuildUI();
        BuildMoodVolume();
        ShowMain();
        if (GameManager.Instance != null) GameManager.Instance.SetMode(GameMode.Menu);

        StartCoroutine(FadeIn());
        // Пульс включаем через кадр — чтобы перебить авто-музыку из AudioManager.Start.
        StartCoroutine(Deferred(() =>
        {
            if (menuDrone != null) AudioManager.PlayMusic(menuDrone, menuDroneVolume);
        }));
    }

    void Update()
    {
        if (mainPanel != null && mainPanel.activeSelf) SwayCamera();

        var kb = Keyboard.current;
        if (kb == null || !kb.escapeKey.wasPressedThisFrame) return;

        var gm = GameManager.Instance;
        if (gm == null) return;

        if (pausePanel != null && pausePanel.activeSelf) Resume();
        else if (gm.Mode == GameMode.Explore) OpenPause();
    }

    // ---------- Переходы ----------
    void ShowMain()
    {
        if (mainPanel != null) mainPanel.SetActive(true);
        if (pausePanel != null) pausePanel.SetActive(false);
    }

    void OnPlay()
    {
        if (mainPanel != null) mainPanel.SetActive(false);
        RemoveMoodVolume();       // игра идёт в штатном грейде
        RestoreCamera();
        AudioManager.PlayBackground(); // вернуть обычную музыку
        // Пропускаем кадр: иначе тот же клик по «Играть» интро прочитает как «пропустить облёт».
        StartCoroutine(Deferred(() =>
        {
            if (intro != null) intro.BeginIntro();
            else if (GameManager.Instance != null) GameManager.Instance.SetMode(GameMode.Explore);
        }));
    }

    void OpenPause()
    {
        if (pausePanel != null) pausePanel.SetActive(true);
        if (GameManager.Instance != null) GameManager.Instance.SetMode(GameMode.Menu);
    }

    void Resume()
    {
        if (pausePanel != null) pausePanel.SetActive(false);
        // Пропускаем кадр: иначе клик по «Продолжить» тут же сработает как интеракт под прицелом.
        StartCoroutine(Deferred(() =>
        {
            if (GameManager.Instance != null) GameManager.Instance.SetMode(GameMode.Explore);
        }));
    }

    IEnumerator Deferred(System.Action action)
    {
        yield return null;
        action?.Invoke();
    }

    void ToMainMenu()
    {
        // Перезагрузка активной сцены полностью сбрасывает счёт/прогресс и снова открывает это меню.
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void Quit()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    // ---------- Проявление из черноты ----------
    IEnumerator FadeIn()
    {
        if (fadeOverlay == null) yield break;
        float t = 0f;
        float dur = Mathf.Max(0.01f, fadeInDuration);
        while (t < dur)
        {
            t += Time.deltaTime;
            float a = 1f - Mathf.Clamp01(t / dur);
            var c = fadeOverlay.color; c.a = a; fadeOverlay.color = c;
            yield return null;
        }
        var end = fadeOverlay.color; end.a = 0f; fadeOverlay.color = end;
        fadeOverlay.gameObject.SetActive(false);
    }

    // ---------- Мрачный грейд (рантайм URP Volume) ----------
    void BuildMoodVolume()
    {
        if (!menuColorGrade) return;

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();

        var color = profile.Add<ColorAdjustments>(true);
        color.saturation.Override(-45f);
        color.contrast.Override(12f);
        color.postExposure.Override(-0.4f);
        color.colorFilter.Override(new Color(0.78f, 0.83f, 0.95f)); // холодный сине-серый

        var vig = profile.Add<Vignette>(true);
        vig.intensity.Override(0.45f);
        vig.smoothness.Override(1f);
        vig.color.Override(Color.black);

        var grain = profile.Add<FilmGrain>(true);
        grain.intensity.Override(0.55f);

        moodVolumeGO = new GameObject("MenuMoodVolume");
        moodVolumeGO.transform.SetParent(transform, false);
        var vol = moodVolumeGO.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.priority = 100f; // поверх штатного Global Volume сцены
        vol.profile = profile;
    }

    void RemoveMoodVolume()
    {
        if (moodVolumeGO != null) Destroy(moodVolumeGO);
        moodVolumeGO = null;
    }

    // ---------- Покачивание камеры на фоне ----------
    void SwayCamera()
    {
        if (!idleCameraSway) return;
        if (!swayCaptured)
        {
            var cam = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            if (cam == null) return;
            swayCam = cam.transform;
            swayCamBaseRot = swayCam.localRotation;
            swayCaptured = true;
        }
        swayTime += Time.deltaTime * swaySpeed;
        float pitch = pitchDownBias + Mathf.Sin(swayTime) * swayAmplitude * 0.5f;
        float yaw = Mathf.Cos(swayTime * 0.8f) * swayAmplitude;
        swayCam.localRotation = swayCamBaseRot * Quaternion.Euler(pitch, yaw, 0f);
    }

    void RestoreCamera()
    {
        if (swayCaptured && swayCam != null) swayCam.localRotation = swayCamBaseRot;
        swayCaptured = false;
    }

    // ---------- Построение UI ----------
    void BuildUI()
    {
        var canvasGO = new GameObject("MainMenuCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200; // поверх остального UI сцены
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        string title = string.IsNullOrWhiteSpace(gameTitle) ? Application.productName : gameTitle;

        var cold = new Color(0.62f, 0.66f, 0.72f, 1f);      // холодный заголовок
        var faded = new Color(0.5f, 0.5f, 0.55f, 0.5f);     // тусклый эпиграф
        var btnText = new Color(0.75f, 0.77f, 0.8f, 1f);    // приглушённые подписи кнопок

        mainPanel = CreatePanel(canvasGO.transform, "MainPanel", 0.72f);
        CreateLabel(mainPanel.transform, title, 78f, FontStyles.Bold, cold, new Vector2(0f, 210f), new Vector2(1400f, 170f));
        if (!string.IsNullOrWhiteSpace(tagline))
            CreateLabel(mainPanel.transform, tagline, 28f, FontStyles.Italic, faded, new Vector2(0f, 120f), new Vector2(1200f, 60f));
        CreateButton(mainPanel.transform, "Играть", btnText, new Vector2(0f, 10f), OnPlay);
        CreateButton(mainPanel.transform, "Выход", btnText, new Vector2(0f, -90f), Quit);

        pausePanel = CreatePanel(canvasGO.transform, "PausePanel", 0.82f);
        CreateLabel(pausePanel.transform, "Пауза", 60f, FontStyles.Bold, cold, new Vector2(0f, 200f), new Vector2(1000f, 130f));
        CreateButton(pausePanel.transform, "Продолжить", btnText, new Vector2(0f, 60f), Resume);
        CreateButton(pausePanel.transform, "В главное меню", btnText, new Vector2(0f, -40f), ToMainMenu);
        CreateButton(pausePanel.transform, "Выход", btnText, new Vector2(0f, -140f), Quit);
        pausePanel.SetActive(false);

        // Верхний чёрный слой для проявления из черноты (не ловит клики).
        var overlay = new GameObject("FadeOverlay", typeof(Image));
        var ort = overlay.GetComponent<RectTransform>();
        ort.SetParent(canvasGO.transform, false);
        ort.anchorMin = Vector2.zero; ort.anchorMax = Vector2.one;
        ort.offsetMin = Vector2.zero; ort.offsetMax = Vector2.zero;
        fadeOverlay = overlay.GetComponent<Image>();
        fadeOverlay.color = Color.black;
        fadeOverlay.raycastTarget = false;
        overlay.transform.SetAsLastSibling();
    }

    GameObject CreatePanel(Transform parent, string name, float alpha)
    {
        var go = new GameObject(name, typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        go.GetComponent<Image>().color = new Color(0.02f, 0.025f, 0.035f, alpha); // холодный, почти чёрный
        return go;
    }

    TextMeshProUGUI CreateLabel(Transform parent, string text, float size, FontStyles style, Color color, Vector2 pos, Vector2 sizeDelta)
    {
        var go = new GameObject("Label", typeof(TextMeshProUGUI));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = sizeDelta;
        var tmp = go.GetComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null) tmp.font = TMP_Settings.defaultFontAsset;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = color;
        tmp.enableWordWrapping = false;
        return tmp;
    }

    Button CreateButton(Transform parent, string label, Color textColor, Vector2 pos, System.Action onClick)
    {
        var go = new GameObject("Button_" + label, typeof(Image), typeof(Button));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(440f, 82f);

        var img = go.GetComponent<Image>();
        img.color = new Color(0.09f, 0.10f, 0.12f, 0.92f); // тёмная холодная заливка

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.55f, 0.58f, 0.6f, 1f); // приглушённо-серый, без «надежды»
        colors.pressedColor = new Color(0.38f, 0.4f, 0.42f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.fadeDuration = 0.25f;
        btn.colors = colors;
        btn.onClick.AddListener(() =>
        {
            if (clickSound != null) AudioManager.PlaySFX(clickSound);
            onClick?.Invoke();
        });

        CreateLabel(go.transform, label, 34f, FontStyles.Normal, textColor, Vector2.zero, new Vector2(420f, 78f));
        return btn;
    }
}
