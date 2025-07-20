using UnityEditor;
using UnityEditor.Presets;
using UnityEngine;
using UnityEngine.UI;

public class HierarchyUtilities
{
    [InitializeOnLoadMethod]
    private static void ObjectChangeEventsExample()
    {
        ObjectChangeEvents.changesPublished -= ChangesPublished;
        ObjectChangeEvents.changesPublished += ChangesPublished;
    }

    static void ChangesPublished(ref ObjectChangeEventStream stream)
    {
        for (int i = 0; i < stream.length; ++i)
        {
            if (stream.GetEventType(i) == ObjectChangeKind.CreateGameObjectHierarchy)
            {
                stream.GetCreateGameObjectHierarchyEvent(i, out var createGameObjectHierarchyEvent);
                var go = EditorUtility.InstanceIDToObject(createGameObjectHierarchyEvent.instanceId) as GameObject;
                if (!go) return;
                var spriteRenderer = go.GetComponent<SpriteRenderer>();
                if (!spriteRenderer) return;
                var canvas = go.GetComponentInParent<Canvas>();
                if (canvas)
                {
                    Undo.RegisterFullObjectHierarchyUndo(go, "Replace SpriteRenderer");
                    var sprite = spriteRenderer.sprite;
                    Object.DestroyImmediate(spriteRenderer);
                    var image = go.AddComponent<Image>();
                    image.transform.localScale = Vector3.one;
                    var presets = Preset.GetDefaultPresetsForType(new PresetType(image));
                    if (presets.Length > 0)
                        presets[0].preset.ApplyTo(image);
                    image.sprite = sprite;
                    image.SetNativeSize();
                    image.rectTransform.position = Vector3.zero;
                    image.rectTransform.anchoredPosition = Vector2.zero;
                }
            }
        }
    }
}
