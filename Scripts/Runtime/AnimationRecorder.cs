using UnityEngine;
using UnityEditor.Animations;
using UnityEngine.Animations;
using UnityEditor;

[ExecuteAlways]
public class AnimationRecorder : MonoBehaviour
{
    public AnimationClip clip;
    public bool recordInEditorTime;

    private GameObjectRecorder recorder;

    void OnEnable()
    {
        if (!recordInEditorTime && !Application.isPlaying) return;
        if (clip == null) return;
        // Create recorder and record the script GameObject.
        recorder = new GameObjectRecorder(gameObject);
        recorder.BindAll(gameObject, false);

        //EditorCurveBinding binding1 = new EditorCurveBinding();
        //binding1.propertyName = "xAxis";
        //recorder.BindComponentsOfType<Cyclist>(gameObject, false);

        //Debug.Log(JsonConvert.SerializeObject(recorder.GetBindings()));
        //recorder.Bind(binding1);
    }

    void LateUpdate()
    {
        if (!recordInEditorTime && !Application.isPlaying) return;
        if (clip == null) return;

        // Take a snapshot and record all the bindings values for this frame.
        recorder.TakeSnapshot(Time.deltaTime);
    }

    void OnDisable()
    {
        if (!recordInEditorTime && !Application.isPlaying) return;
        if (clip == null) return;

        if (recorder.isRecording)
        {
            Debug.Log("Animation saved");
            // Save the recorded session to the clip.
            recorder.SaveToClip(clip);
        }
    }
}