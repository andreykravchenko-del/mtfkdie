using UnityEngine;

/// <summary>
/// Запускает набор реплик: автоматически на старте (вступительный монолог) либо вручную
/// через Play() / OnTriggerEnter (по тегу игрока).
/// </summary>
public class NarrativeTrigger : MonoBehaviour
{
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private bool playOnPlayerEnter = false;
    [SerializeField] private bool onlyOnce = true;
    [TextArea(2, 4)] [SerializeField] private string[] lines;

    private bool played;

    void Start()
    {
        if (playOnStart) Play();
    }

    void OnTriggerEnter(Collider other)
    {
        if (playOnPlayerEnter && other.CompareTag("Player")) Play();
    }

    public void Play()
    {
        if ((onlyOnce && played) || lines == null || lines.Length == 0) return;
        if (NarrativeSystem.Instance == null) return;
        played = true;
        NarrativeSystem.Instance.Play(lines);
    }
}
