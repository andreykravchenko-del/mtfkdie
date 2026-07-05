using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using TMPro;

/// <summary>
/// Главное меню + пауза поверх живой игровой сцены.
/// UI больше не строится кодом — иерархия лежит в префабе (MainMenu.prefab), а сюда прокидываются
/// ссылки на панели/кнопки/лейблы через инспектор. Контроллер отвечает только за логику:
///   • «Играть» — снимает мрачный грейд, возвращает обычную музыку и запускает вступление;
///   • Esc в игре (Explore) — пауза; Esc в паузе — продолжить;
///   • «В главное меню» — перезагружает сцену (полный сброс прогресса → снова это меню).
/// Фон меню — сама квартира за полупрозрачной панелью: холодный грейд (рантайм URP Volume),
/// медленное проявление из черноты, тяжёлый дрейф камеры и давящий пульс сердца вместо музыки.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("Заголовок")]
    [Tooltip("Заголовок в меню. Пусто → название продукта из Player Settings.")]
    [SerializeField] private string gameTitle = "";
    [Tooltip("Тусклая строка-эпиграф под заголовком. Пусто → эпиграф скрывается.")]
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

    [Header("UI — ссылки из префаба")]
    [Tooltip("Панель главного меню (заголовок + Играть/Выход).")]
    [SerializeField] private GameObject mainPanel;
    [Tooltip("Панель паузы (Продолжить/В меню/Выход).")]
    [SerializeField] private GameObject pausePanel;
    [Tooltip("Чёрный слой на весь экран для проявления из черноты.")]
    [SerializeField] private Image fadeOverlay;
    [Tooltip("Лейбл заголовка (текст выставляется из gameTitle).")]
    [SerializeField] private TMP_Text titleLabel;
    [Tooltip("Лейбл эпиграфа (текст выставляется из tagline; скрывается, если пусто).")]
    [SerializeField] private TMP_Text taglineLabel;

    [Header("Кнопки — ссылки из префаба")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button quitButtonMain;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private Button quitButtonPause;

    private IntroSequence intro;
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
        ApplyTexts();
        WireButtons();
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

    // ---------- Настройка UI из ссылок ----------
    void ApplyTexts()
    {
        if (titleLabel != null)
            titleLabel.text = string.IsNullOrWhiteSpace(gameTitle) ? Application.productName : gameTitle;

        if (taglineLabel != null)
        {
            bool hasTagline = !string.IsNullOrWhiteSpace(tagline);
            taglineLabel.gameObject.SetActive(hasTagline);
            if (hasTagline) taglineLabel.text = tagline;
        }
    }

    void WireButtons()
    {
        Wire(playButton, OnPlay);
        Wire(quitButtonMain, Quit);
        Wire(resumeButton, Resume);
        Wire(mainMenuButton, ToMainMenu);
        Wire(quitButtonPause, Quit);
    }

    void Wire(Button btn, System.Action action)
    {
        if (btn == null) return;
        btn.onClick.AddListener(() =>
        {
            if (clickSound != null) AudioManager.PlaySFX(clickSound);
            action?.Invoke();
        });
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
        fadeOverlay.gameObject.SetActive(true);
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
}
