using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEditor.Presets;

public class ComponentUtilities
{
    private static SerializedObject savedObject;

    [InitializeOnLoadMethod]
    static void Initialize()
    {
        EditorApplication.contextualPropertyMenu += CopyOnContextClickWithModifiers;
    }

    public static void CopyOnContextClickWithModifiers(GenericMenu menu, SerializedProperty property)
    {
        if (Event.current.modifiers == EventModifiers.Control)
        {
            savedObject = new SerializedObject(property.serializedObject.targetObject);
            Debug.Log("Component copied");
        }
        // Paste properties with same names
        if (Event.current.modifiers == (EventModifiers.Shift | EventModifiers.Control))
        {
            if (savedObject == null)
            {
                Debug.Log("Saved component is null");
                return;
            }
            PartialPaste(property);
        }
        else if (Event.current.modifiers == EventModifiers.Shift)
        {
            if (savedObject == null || savedObject.targetObject == null)
            {
                Debug.Log("Saved component targetObject is null");
                return;
            }

            // Paste values if type is the same
            if (savedObject.targetObject.GetType() == property.serializedObject.targetObject.GetType())
            {
                Undo.RecordObject(property.serializedObject.targetObject, "Paste component values");
                EditorUtility.CopySerialized(savedObject.targetObject, property.serializedObject.targetObject);
                Debug.Log("Component values pasted");
            }
            // Create new component
            else
            {
                GameObject g = ((Component)property.serializedObject.targetObject).gameObject;
                Component c = Undo.AddComponent(g, savedObject.targetObject.GetType());
                EditorUtility.CopySerialized(savedObject.targetObject, c);
                Debug.Log("Component pasted as new");
            }

        }
        // Delete Component
        if (Event.current.modifiers == EventModifiers.Alt)
        {
            Undo.RegisterFullObjectHierarchyUndo(property.serializedObject.targetObject, "Delete component");
            EditorApplication.delayCall += () => { Object.DestroyImmediate(property.serializedObject.targetObject); };
        }
    }

    private static void PartialPaste(SerializedProperty property)
    {
        Undo.RecordObject(property.serializedObject.targetObject, "Paste component values partially");
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
        Debug.Log("Component values partially pasted");
    }
}
