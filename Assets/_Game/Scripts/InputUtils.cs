using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Общие хелперы для чтения ввода мыши.
/// На WebGL Input System отдаёт Mouse.delta в другом (гораздо меньшем) масштабе,
/// чем на десктопе: браузер шлёт movementX/Y в CSS-пикселях без той нормализации,
/// что применяется в стандалон-билдах. Из-за этого при той же чувствительности
/// в вебе камера крутится еле-еле. Компенсируем множителем для WebGL.
/// </summary>
public static class InputUtils
{
    // Подобрано так, чтобы веб-чувствительность визуально совпадала со стандалоном.
    // При необходимости крутится одной цифрой.
    public const float WebGLLookScale = 5f;

    /// <summary>
    /// Дельта мыши за кадр с платформенной коррекцией. Используем везде,
    /// где вращаем камеру/объект по движению мыши, чтобы чувствительность
    /// была одинаковой на всех платформах.
    /// </summary>
    public static Vector2 MouseLookDelta()
    {
        var mouse = Mouse.current;
        if (mouse == null) return Vector2.zero;

        Vector2 delta = mouse.delta.ReadValue();
#if UNITY_WEBGL && !UNITY_EDITOR
        delta *= WebGLLookScale;
#endif
        return delta;
    }
}
