using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Показ монологов/нарратива с эффектом печатной машинки.
///   • клик / пробел / Enter / E, пока строка печатается → показать строку целиком (пропуск);
///   • тот же ввод на готовой строке → следующая реплика;
///   • зажать E → быстрое пролистывание (ускорение печати).
/// По окончании очереди возвращает заданный режим (обычно Explore).
/// (UI-логика и данные объединены в один скрипт для скорости — отдельный DialogueUI не нужен.)
/// </summary>
public class NarrativeSystem : MonoBehaviour
{
    public static NarrativeSystem Instance { get; private set; }

    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text text;
    [SerializeField] private float charsPerSecond = 40f;
    [SerializeField] private float fastMultiplier = 4f;
    [SerializeField] private AudioClip advanceClip;
    [Tooltip("Невнятный бубнёж — зациклённый источник, играет, пока печатается реплика.")]
    [SerializeField] private AudioSource mumbleSource;

    private readonly Queue<string> queue = new Queue<string>();
    private string currentLine = "";
    private float revealed;
    private bool active;
    private GameMode returnMode = GameMode.Explore;

    public bool IsActive => active;

    void Awake()
    {
        Instance = this;
        if (panel != null) panel.SetActive(false);
    }

    public void Play(IEnumerable<string> lines, GameMode modeToReturn = GameMode.Explore)
    {
        queue.Clear();
        foreach (var l in lines) queue.Enqueue(l);
        if (queue.Count == 0) return;

        returnMode = modeToReturn;
        active = true;
        GameManager.Instance.SetMode(GameMode.Narrative);
        if (panel != null) panel.SetActive(true);
        NextLine();
    }

    void NextLine()
    {
        currentLine = queue.Dequeue();
        revealed = 0f;
        if (text != null) text.text = "";
    }

    void Update()
    {
        if (!active) return;

        bool fast = Keyboard.current != null && Keyboard.current.eKey.isPressed;
        bool typing = revealed < currentLine.Length;

        SetMumble(typing); // бубнёж, пока идёт печать реплики

        if (typing)
        {
            revealed += charsPerSecond * (fast ? fastMultiplier : 1f) * Time.deltaTime;
            int show = Mathf.Clamp(Mathf.FloorToInt(revealed), 0, currentLine.Length);
            if (text != null) text.text = currentLine.Substring(0, show);
        }

        if (!AdvancePressed()) return;

        if (typing)
        {
            revealed = currentLine.Length; // мгновенно дописать
            if (text != null) text.text = currentLine;
            SetMumble(false);
        }
        else
        {
            AudioManager.PlaySFX(advanceClip);
            if (queue.Count > 0) NextLine();
            else End();
        }
    }

    void End()
    {
        active = false;
        SetMumble(false);
        if (panel != null) panel.SetActive(false);
        GameManager.Instance.SetMode(returnMode);
    }

    void SetMumble(bool on)
    {
        if (mumbleSource == null) return;
        if (on && !mumbleSource.isPlaying) mumbleSource.Play();
        else if (!on && mumbleSource.isPlaying) mumbleSource.Stop();
    }

    static bool AdvancePressed()
    {
        var kb = Keyboard.current;
        var m = Mouse.current;
        return (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame))
            || (m != null && m.leftButton.wasPressedThisFrame);
    }
}
