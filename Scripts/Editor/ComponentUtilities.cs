using UnityEditor;
using UnityEngine;

public class ComponentUtilities
{
    private static SerializedObject savedSerializedObject;
    private static Object savedTargetObject;

    // Property copying
    private static SerializedObject savedSerializedObjectForProperty;
    private static string savedPropertyPath;

    [InitializeOnLoadMethod]
    static void Initialize()
    {
        EditorApplication.contextualPropertyMenu += CopyOnContextClickWithModifiers;
    }

    public static void CopyOnContextClickWithModifiers(GenericMenu menu, SerializedProperty property)
    {
        AddCopyPastePropertyOption(menu, property);
        // Ctrl == copy, shift == paste, alt == copy, ctrl + alt == cut, ctrl + shift == adaptive paste
        var modifiers = Event.current.modifiers;
        if (modifiers == EventModifiers.Control || modifiers == (EventModifiers.Control | EventModifiers.Alt))
        {
            savedSerializedObject = new SerializedObject(property.serializedObject.targetObject);
            // savedObject.targetObject is null if we delete component in the meantime. Hence we create a copy
            savedTargetObject = Object.Instantiate(property.serializedObject.targetObject);
            if (savedTargetObject is Component component) // Can be a scriptableObject
                component.gameObject.hideFlags = HideFlags.HideAndDontSave;

            Debug.Log("Component copied");
        }
        // Paste properties with same names
        if (modifiers == (EventModifiers.Shift | EventModifiers.Control))
        {
            if (savedSerializedObject == null)
            {
                Debug.Log("Saved component is null");
                return;
            }
            AdaptivePaste(property);
        }
        else if (modifiers == EventModifiers.Shift)
        {
            if (savedSerializedObject == null)
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

    /// <summary> Tries to find properties with the same name and type and copypastes values </summary>
    private static void AdaptivePaste(SerializedProperty property)
    {
        Undo.RecordObject(property.serializedObject.targetObject, "Paste component values adaptively");
        SerializedObject destination = new SerializedObject(property.serializedObject.targetObject);
        SerializedProperty sourceProperties = savedSerializedObject.GetIterator();

        // This if statement will skip script type so that we don't override the destination component's type
        if (sourceProperties.NextVisible(true))
        {
            while (sourceProperties.NextVisible(true)) // Iterate through all serializedProperties
            {
                // Find corresponding property in destination component by path
                SerializedProperty destinationProperty = destination.FindProperty(sourceProperties.propertyPath);

                // Validate that the properties are present in both components, and that they're the same type
                if (destinationProperty != null && destinationProperty.propertyType == sourceProperties.propertyType)
                {
                    // Copy value from savedObject to destination component
                    destination.CopyFromSerializedProperty(sourceProperties);
                }
            }
        }
        destination.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
        Debug.Log("Component values adaptively pasted");
    }

    private static void AddCopyPastePropertyOption(GenericMenu menu, SerializedProperty property)
    {
        var propertyPath = property.propertyPath;
        menu.AddItem(new GUIContent("Copy Property"), false,
            () => CopyProperty(property.serializedObject, propertyPath));
        if (savedSerializedObjectForProperty != null)
        {
            menu.AddItem(new GUIContent("Paste Property"), false,
                () => PasteProperty(property.serializedObject, propertyPath));
            menu.AddItem(new GUIContent("Paste Property Adaptively"), false,
                () => PastePropertyAdaptively(property.serializedObject, propertyPath));
        }
        else
            menu.AddDisabledItem(new GUIContent("Paste Property"), false);
    }

    private static void CopyProperty(SerializedObject serializedObject, string propertyPath)
    {
        savedPropertyPath = propertyPath;
        savedSerializedObjectForProperty = new SerializedObject(serializedObject.targetObject);
    }
    private static void PasteProperty(SerializedObject destination, string propertyPath)
    {
        var sourceProperty = savedSerializedObjectForProperty.FindProperty(savedPropertyPath);

        destination.CopyFromSerializedProperty(sourceProperty);
        destination.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
    }

    /// <summary>
    /// Paste only child properties of a property, useful for SerializeReference properties pasting to not change type
    /// </summary>
    private static void PastePropertyAdaptively(SerializedObject destination, string propertyPath)
    {
        Undo.RecordObject(destination.targetObject, "Paste property values adaptively");
        var sourceProperty = savedSerializedObjectForProperty.FindProperty(savedPropertyPath);
        if (sourceProperty.propertyType != SerializedPropertyType.ManagedReference)
        {
            PasteProperty(destination, propertyPath);
            return;
        }

        foreach (SerializedProperty prop in sourceProperty) // Iterate through all serializedProperties
        {
            // Find corresponding property in destination component by name
            SerializedProperty destinationProperty = destination.FindProperty(propertyPath + "." + prop.name);

            // Validate that the properties are present in both components, and that they're the same type
            if (destinationProperty != null && destinationProperty.propertyType == prop.propertyType)
            {
                // Copy value from savedObject to destination component
                destination.CopyFromSerializedProperty(prop);
            }
        }
        destination.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
    }
}
