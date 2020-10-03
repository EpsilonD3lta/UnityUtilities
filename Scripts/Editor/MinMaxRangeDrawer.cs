using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(MinMaxFloat), true)]
public class MinMaxRangeDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        label = EditorGUI.BeginProperty(position, label, property);
        position = EditorGUI.PrefixLabel(position, label);

        SerializedProperty minProp = property.FindPropertyRelative("min");
        SerializedProperty maxProp = property.FindPropertyRelative("max");

        float min = minProp.floatValue;
        float max = maxProp.floatValue;

        float rangeMin = 0;
        float rangeMax = 1;

        var ranges = (MinMaxRangeAttribute[])fieldInfo.GetCustomAttributes(typeof(MinMaxRangeAttribute), true);
        if (ranges.Length > 0)
        {
            rangeMin = ranges[0].Min;
            rangeMax = ranges[0].Max;
        }

        EditorGUI.BeginChangeCheck();

        const float floatFieldRectWidth = 50f;
        // Min value
        var minFloatFieldRect = new Rect(position);
        minFloatFieldRect.width = floatFieldRectWidth;
        min = EditorGUI.DelayedFloatField(minFloatFieldRect, min);
        min = Mathf.Min(min, max);
        position.xMin += floatFieldRectWidth + 5;

        // Max value
        var maxFloatFieldRect = new Rect(position);
        maxFloatFieldRect.xMin = maxFloatFieldRect.xMax - floatFieldRectWidth;
        max = EditorGUI.DelayedFloatField(maxFloatFieldRect, max);
        max = Mathf.Max(min, max);
        position.xMax -= floatFieldRectWidth + 5;

        //Slider
        EditorGUI.MinMaxSlider(position, ref min, ref max, rangeMin, rangeMax);

        if (EditorGUI.EndChangeCheck())
        {
            minProp.floatValue = min;
            maxProp.floatValue = max;
        }

        EditorGUI.EndProperty();
    }
}


