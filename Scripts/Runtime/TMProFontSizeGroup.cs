using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Inspired by https://forum.unity.com/threads/textmeshpro-precull-dorebuilds-performance.762968/#post-5083490
/// </summary>
public class TMProFontSizeGroup : MonoBehaviour
{
    public List<TMP_Text> tmpTexts = new List<TMP_Text>();
    public bool alsoSetChildren;

    private bool isForceUpdatingMesh;

    private void Awake()
    {
        if (alsoSetChildren)
        {
            tmpTexts.AddRange(GetComponentsInChildren<TMP_Text>());
        }
        foreach (TMP_Text tmpText in tmpTexts)
        {
            tmpText.enableAutoSizing = false;
        }

        SetFontsize();
    }

    private void OnEnable()
    {
       TMPro_EventManager.TEXT_CHANGED_EVENT.Add(ReactToTextChanged);
    }

    private void OnDisable()
    {
       TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(ReactToTextChanged);
    }

    private void ReactToTextChanged(Object obj)
    {
        TMP_Text tmpText = obj as TMP_Text;
        if (tmpText != null && tmpTexts.Contains(tmpText) && !isForceUpdatingMesh) SetFontsize();
    }
    private void SetFontsize()
    {
        if (tmpTexts == null || tmpTexts.Count == 0) return;
        // Iterate over each of the text objects in the array to find a good test candidate
        // There are different ways to figure out the best candidate
        // Preferred width works fine for single line text objects
        int candidateIndex = 0;
        float maxPreferredWidth = 0;

        for (int i = 0; i < tmpTexts.Count; i++)
        {
            float preferredWidth = tmpTexts[i].preferredWidth;
            if (preferredWidth > maxPreferredWidth)
            {
                maxPreferredWidth = preferredWidth;
                candidateIndex = i;
            }
        }

        // Force an update of the candidate text object so we can retrieve its optimum point size.
        tmpTexts[candidateIndex].enableAutoSizing = true;
        isForceUpdatingMesh = true;
        tmpTexts[candidateIndex].ForceMeshUpdate();

        float optimumPointSize = tmpTexts[candidateIndex].fontSize;

        // Disable auto size on our test candidate
        tmpTexts[candidateIndex].enableAutoSizing = false;

        // Iterate over all the text objects to set the point size
        for (int i = 0; i < tmpTexts.Count; i++)
        {
            tmpTexts[i].fontSize = optimumPointSize;
            tmpTexts[i].ForceMeshUpdate();
        }
        isForceUpdatingMesh = false;
    }
}
