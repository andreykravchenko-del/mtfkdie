using UnityEngine;

public enum MemoryKind { Good, Bad }

/// <summary>
/// Данные одного осколка памяти. Создаётся: ПКМ в Project ▸ Create ▸ Game ▸ Memory Data.
/// </summary>
[CreateAssetMenu(fileName = "Memory", menuName = "Game/Memory Data")]
public class MemoryData : ScriptableObject
{
    [Header("Отображение")]
    public string displayName = "Предмет";
    [TextArea(3, 6)] public string description;

    [Header("Влияние на исход")]
    public MemoryKind kind = MemoryKind.Good;
    [Min(0)] public int points = 1;

    [Header("Запоминание")]
    [Tooltip("Сколько секунд держать E, чтобы запомнить предмет.")]
    [Min(0.1f)] public float captureDuration = 3f;

    [Tooltip("Реплики ГГ, всплывающие по мере заполнения шкалы (равномерно распределяются).")]
    [TextArea(2, 3)] public string[] captureComments;

    [Header("Воспоминание (показывается при успехе)")]
    public Sprite memoryImage;
    // Задел на будущее: public UnityEngine.Video.VideoClip memoryVideo;

    [Header("Звуки предмета")]
    [Tooltip("Шум самого предмета (шорох, мяуканье и т.п.) — играет при открытии осмотра.")]
    public AudioClip itemSound;
    [Tooltip("Звук воспоминания — играет вместе с картинкой при успешном запоминании.")]
    public AudioClip revealSound;
}
