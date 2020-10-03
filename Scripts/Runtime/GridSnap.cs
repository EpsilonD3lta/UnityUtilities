using UnityEditor;
using UnityEngine;

[ExecuteAlways]
public class GridSnap : MonoBehaviour
{
#if UNITY_EDITOR
    public float gridStep = 1;

    private void Update()
    {
        if (Application.isPlaying) return;
        if (transform.hasChanged)
        {
            transform.position = new Vector3(
                Mathf.RoundToInt(transform.position.x / gridStep) * gridStep,
                Mathf.RoundToInt(transform.position.y / gridStep) * gridStep,
                Mathf.RoundToInt(transform.position.z / gridStep) * gridStep);
            Physics2D.SyncTransforms();
            Undo.RecordObject(gameObject, "Transform Change");
        }
    }

    [CustomEditor(typeof(GridSnap))]
    public class GridSnapEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var gridSnap = (GridSnap)target;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("RotateLeft")) gridSnap.transform.Rotate(0, 0, 90);
            if (GUILayout.Button("RotateRight")) gridSnap.transform.Rotate(0, 0, -90);
            GUILayout.EndHorizontal();

        }
    }
#endif
}
