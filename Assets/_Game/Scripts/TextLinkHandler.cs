using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Вешается на объект с TMP_Text (TextMeshProUGUI).
/// Открывает гиперссылки, размеченные тегом &lt;link="https://..."&gt;текст&lt;/link&gt;.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class TextLinkHandler : MonoBehaviour, IPointerClickHandler
{
    private TMP_Text text;

    private void Awake()
    {
        text = GetComponent<TMP_Text>();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // pressEventCamera — та камера, которой рейкастился этот UI-элемент
        // (null для Screen Space - Overlay), поэтому координаты всегда совпадают.
        int linkIndex = TMP_TextUtilities.FindIntersectingLink(text, eventData.position, eventData.pressEventCamera);
        if (linkIndex < 0)
            return;

        string url = text.textInfo.linkInfo[linkIndex].GetLinkID();
        if (!string.IsNullOrEmpty(url))
            Application.OpenURL(url);
    }
}
