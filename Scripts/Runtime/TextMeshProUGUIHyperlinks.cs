using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

// Inspired by https://www.feelouttheform.net/unity3d-links-textmeshpro/
[RequireComponent(typeof(TextMeshProUGUI))]
public class TextMeshProUGUIHyperlinks : MonoBehaviour, IPointerClickHandler
{
    TextMeshProUGUI textMeshPro;

    void Awake()
    {
        textMeshPro = GetComponent<TextMeshProUGUI>();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        var cam = Camera.main;
        if (textMeshPro.canvas.renderMode == RenderMode.ScreenSpaceOverlay) cam = null;
        else if (textMeshPro.canvas.worldCamera != null) cam = textMeshPro.canvas.worldCamera;

        int linkIndex = TMP_TextUtilities.FindIntersectingLink(textMeshPro, Input.mousePosition, cam);
        if (linkIndex != -1) // Was a link clicked?
        {
            TMP_LinkInfo linkInfo = textMeshPro.textInfo.linkInfo[linkIndex];
            Application.OpenURL(linkInfo.GetLinkID());
        }
    }
}