using UnityEditor;
using UnityEngine;

public class ComponentUtilities
{
    private static SerializedObject savedObject;
    private static Object savedTargetObject;

    [InitializeOnLoadMethod]
    static void Initialize()
    {
        EditorApplication.contextualPropertyMenu += CopyOnContextClickWithModifiers;
    }

    public static void CopyOnContextClickWithModifiers(GenericMenu menu, SerializedProperty property)
    {
        // Ctrl == copy, shift == paste, alt == copy, ctrl + alt == cut, ctrl + shift == adaptive paste
        var modifiers = Event.current.modifiers;
        if (modifiers == EventModifiers.Control || modifiers == (EventModifiers.Control | EventModifiers.Alt))
        {
            savedObject = new SerializedObject(property.serializedObject.targetObject);
            // savedObject.targetObject is null if we delete component in the meantime. Hence we create a copy
            savedTargetObject = Object.Instantiate(property.serializedObject.targetObject);
            if (savedTargetObject is Component component) // Can be a scriptableObject
                component.gameObject.hideFlags = HideFlags.HideAndDontSave;

            Debug.Log("Component copied");
        }
        // Paste properties with same names
        if (modifiers == (EventModifiers.Shift | EventModifiers.Control))
        {
            if (savedObject == null)
            {
                Debug.Log("Saved component is null");
                return;
            }
            AdaptivePaste(property);
        }
        else if (modifiers == EventModifiers.Shift)
        {
            if (savedObject == null)
            {
                Debug.Log("Saved component is null");
                return;
            }
            if (savedTargetObject == null)
            {
                Debug.Log("Saved component target object is null");
                return;
            }

            // Paste values if type is the same
            if (savedTargetObject.GetType() == property.serializedObject.targetObject.GetType())
            {
                Undo.RecordObject(property.serializedObject.targetObject, "Paste component values");
                EditorUtility.CopySerialized(savedTargetObject, property.serializedObject.targetObject);
                Debug.Log("Component values pasted");
            }
            // Create new component
            else
            {
                GameObject g = ((Component)property.serializedObject.targetObject).gameObject;
                Component c = Undo.AddComponent(g, savedTargetObject.GetType());
                EditorUtility.CopySerialized(savedTargetObject, c);
                Debug.Log("Component pasted as new");
            }

        }
        // Delete Component
        if ((modifiers & EventModifiers.Alt) == EventModifiers.Alt)
        {
            Undo.RegisterFullObjectHierarchyUndo(property.serializedObject.targetObject, "Delete component");
            int undoID = Undo.GetCurrentGroup();
            System.Type type = property.serializedObject.targetObject.GetType();
            EditorApplication.delayCall += () => { Object.DestroyImmediate(property.serializedObject.targetObject); };
            foreach (var go in Selection.gameObjects)
            {
                if (go == property.serializedObject.targetObject) continue;
                var component = go.GetComponent(type);
                if (component)
                {
                    Undo.RegisterFullObjectHierarchyUndo(go, "Delete component");
                    Undo.CollapseUndoOperations(undoID);
                    EditorApplication.delayCall += () => { Object.DestroyImmediate(component); };
                }
            }
        }
    }

    /// <summary>
    /// Tries to find properties with the same name and type and copypastes values
    /// </summary>
    /// <param name="property"></param>
    private static void AdaptivePaste(SerializedProperty property)
    {
        Undo.RecordObject(property.serializedObject.targetObject, "Paste component values adaptively");
        SerializedObject destination = new SerializedObject(property.serializedObject.targetObject);
        SerializedProperty savedObjectProperties = savedObject.GetIterator();

        // This if statement will skip script type so that we don't override the destination component's type
        if (savedObjectProperties.NextVisible(true))
        {
            while (savedObjectProperties.NextVisible(true)) // Iterate through all serializedProperties
            {
                // Find corresponding property in destination component by name
                SerializedProperty destinationProperty = destination.FindProperty(savedObjectProperties.name);

                // Validate that the properties are present in both components, and that they're the same type
                if (destinationProperty != null && destinationProperty.propertyType == savedObjectProperties.propertyType)
                {
                    // Copy value from savedObject to destination component
                    destination.CopyFromSerializedProperty(savedObjectProperties);
                }
            }
        }
        destination.ApplyModifiedProperties();
        Debug.Log("Component values adaptively pasted");
    }
}
