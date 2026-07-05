using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Вступление: плавный облёт комнаты и ГГ по заданным ракурсам (waypoints).
/// Гоняем ПО ТОЧКАМ саму игровую камеру (flyCamera = Main Camera), а не отдельную —
/// так пролёт гарантированно виден (одна экранная камера, без конфликтов рендера).
/// По окончании возвращаем камеру на место игрока и (опц.) показываем монолог, затем Explore.
/// Пропускается любой клавишей/кликом.
/// </summary>
public class IntroSequence : MonoBehaviour
{
    [Tooltip("Камера, которой облетаем комнату. Обычно Main Camera игрока.")]
    [SerializeField] private Camera flyCamera;
    [SerializeField] private Transform[] waypoints;
    [Tooltip("Секунд на переезд между соседними ракурсами.")]
    [SerializeField] private float moveDuration = 3.5f;
    [Tooltip("Секунд задержки на каждом ракурсе.")]
    [SerializeField] private float holdDuration = 1.5f;
    [SerializeField] private bool skippable = true;
    [Tooltip("Запускать облёт сам, на старте сцены. Главное меню выключает это и вызывает облёт по кнопке «Играть».")]
    [SerializeField] private bool playOnStart = true;
    [TextArea(2, 4)] [SerializeField] private string[] introLines;
    [SerializeField] private AudioClip introMusic;

    private bool finished;
    private Transform camTf;
    private Vector3 origLocalPos;
    private Quaternion origLocalRot;

    /// Отключить авто-старт облёта (вызывает MainMenuController в Awake, до Start интро).
    public void DisableAutoPlay() => playOnStart = false;

    void Start()
    {
        if (playOnStart) BeginIntro();
    }

    /// Запустить вступительный облёт. Вызывается из Start (авто) или из главного меню.
    public void BeginIntro()
    {
        if (flyCamera == null) flyCamera = Camera.main;
        if (flyCamera == null || waypoints == null || waypoints.Length == 0)
        {
            Finish();
            return;
        }

        camTf = flyCamera.transform;
        origLocalPos = camTf.localPosition;
        origLocalRot = camTf.localRotation;

        GameManager.Instance.SetMode(GameMode.Intro);
        if (introMusic != null) AudioManager.PlayMusic(introMusic);
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        camTf.SetPositionAndRotation(waypoints[0].position, waypoints[0].rotation);

        for (int i = 1; i < waypoints.Length; i++)
        {
            float hold = 0f;
            while (hold < holdDuration)
            {
                if (Skipped()) { Finish(); yield break; }
                hold += Time.deltaTime;
                yield return null;
            }

            Vector3 p0 = waypoints[i - 1].position, p1 = waypoints[i].position;
            Quaternion r0 = waypoints[i - 1].rotation, r1 = waypoints[i].rotation;
            float t = 0f;
            while (t < 1f)
            {
                if (Skipped()) { Finish(); yield break; }
                t += Time.deltaTime / Mathf.Max(0.1f, moveDuration);
                float s = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
                camTf.SetPositionAndRotation(Vector3.Lerp(p0, p1, s), Quaternion.Slerp(r0, r1, s));
                yield return null;
            }
        }

        float last = 0f;
        while (last < holdDuration)
        {
            if (Skipped()) { Finish(); yield break; }
            last += Time.deltaTime;
            yield return null;
        }

        Finish();
    }

    bool Skipped()
    {
        if (!skippable) return false;
        var kb = Keyboard.current;
        var m = Mouse.current;
        return (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame))
            || (m != null && m.leftButton.wasPressedThisFrame);
    }

    void Finish()
    {
        if (finished) return;
        finished = true;

        // вернуть камеру на место игрока
        if (camTf != null)
        {
            camTf.localPosition = origLocalPos;
            camTf.localRotation = origLocalRot;
        }

        if (introLines != null && introLines.Length > 0 && NarrativeSystem.Instance != null)
            NarrativeSystem.Instance.Play(introLines); // сам вернёт Explore по завершении
        else
            GameManager.Instance.SetMode(GameMode.Explore);
    }
}
