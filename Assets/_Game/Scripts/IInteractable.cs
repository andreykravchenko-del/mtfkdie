/// <summary>
/// Всё, на что можно навести прицел и нажать E: предмет памяти, кровать и т.п.
/// </summary>
public interface IInteractable
{
    /// Короткая подсказка ("Стакан", "Лечь") — префикс "ЛКМ — " добавляет UI.
    string Prompt { get; }

    void SetHighlight(bool on);

    void Interact(PlayerInteractor interactor);
}
