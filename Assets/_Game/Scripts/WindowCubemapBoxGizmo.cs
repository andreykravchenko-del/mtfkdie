using UnityEngine;

/// <summary>
/// Рисует в сцене бокс-объём двора, который использует шейдер
/// Custom/WindowBoxProjectedCubemap, и прокидывает его размер/смещение в материал.
/// Вешается на тот же объект, что и плоскость-«стекло» окна.
/// Двигая ползунки Box Size / Box Offset, подгоняешь параллакс на глаз.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(Renderer))]
public class WindowCubemapBoxGizmo : MonoBehaviour
{
    [Tooltip("Размер бокса-объёма двора в метрах (XYZ).")]
    public Vector3 boxSize = new Vector3(30f, 15f, 30f);

    [Tooltip("Смещение центра бокса относительно окна.")]
    public Vector3 boxOffset = Vector3.zero;

    [Tooltip("Показывать ли гизмо всегда (иначе только при выделении).")]
    public bool alwaysDrawGizmo = true;

    static readonly int BoxSizeID = Shader.PropertyToID("_BoxSize");
    static readonly int BoxOffsetID = Shader.PropertyToID("_BoxOffset");

    MaterialPropertyBlock _mpb;

    void OnEnable() => Apply();
    void OnValidate() => Apply();

    void Apply()
    {
        var r = GetComponent<Renderer>();
        if (r == null) return;

        _mpb ??= new MaterialPropertyBlock();
        r.GetPropertyBlock(_mpb);
        _mpb.SetVector(BoxSizeID, boxSize);
        _mpb.SetVector(BoxOffsetID, new Vector4(boxOffset.x, boxOffset.y, boxOffset.z, 0f));
        r.SetPropertyBlock(_mpb);
    }

    void OnDrawGizmos()
    {
        if (alwaysDrawGizmo) DrawBox(new Color(0.3f, 0.8f, 1f, 0.9f));
    }

    void OnDrawGizmosSelected()
    {
        if (!alwaysDrawGizmo) DrawBox(new Color(0.3f, 0.8f, 1f, 0.9f));
    }

    void DrawBox(Color color)
    {
        Gizmos.color = color;
        Gizmos.DrawWireCube(transform.position + boxOffset, boxSize);
    }
}
