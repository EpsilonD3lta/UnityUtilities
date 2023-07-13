using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Script execution order should be set to -1 or less
/// </summary>
public class TouchInput : MonoBehaviour
{
    public Finger this[int i]
    {
        get { return fingers[i]; }
    }

    public class Finger
    {
        public event Action<Finger> touchStarted, touchStayed, touchMoved, touchEnded;
        public event Action<Finger> dragStarted, dragged, dragEnded;
        public Vector2 pressPosition;
        public Touch touch;

        public void InvokeTouchStarted() => touchStarted?.Invoke(this);
        public void InvokeTouchStayed() => touchStayed?.Invoke(this);
        public void InvokeTouchMoved() => touchMoved?.Invoke(this);
        public void InvokeTouchEnded() => touchEnded?.Invoke(this);
        public void InvokeDragStarted() => dragStarted?.Invoke(this);
        public void InvokeDragged() => dragged?.Invoke(this);
        public void InvokeDragEnded() => dragEnded?.Invoke(this);
    }

    public event Action<Finger> touchStarted, touchStayed, touchMoved, touchEnded;
    public event Action<Finger> dragStarted, dragged, dragEnded;

    private Dictionary<int, Finger> fingers = new Dictionary<int, Finger>();
    private Dictionary<int, Finger> drags = new Dictionary<int, Finger>();

    private void Update()
    {
        for (int i = 0; i < Input.touchCount; i++)
        {
            var touch = Input.GetTouch(i);
            switch (touch.phase)
            {
                case TouchPhase.Began:
                    var finger = new Finger()
                    {
                        pressPosition = touch.position,
                        touch = touch
                    };
                    fingers[touch.fingerId] = finger;
                    touchStarted?.Invoke(finger);
                    finger.InvokeTouchStarted();
                    break;
                case TouchPhase.Stationary:
                    finger = fingers[touch.fingerId];
                    finger.touch = touch;
                    touchStayed?.Invoke(finger);
                    finger.InvokeTouchStayed();
                    break;
                case TouchPhase.Moved:
                    finger = fingers[touch.fingerId];
                    finger.touch = touch;
                    touchMoved?.Invoke(fingers[touch.fingerId]);
                    finger.InvokeTouchMoved();
                    HandleDrags(fingers[touch.fingerId]);
                    break;
                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    finger = fingers[touch.fingerId];
                    finger.touch = touch;
                    touchEnded?.Invoke(finger);
                    if (drags.ContainsKey(touch.fingerId))
                    {
                        dragEnded?.Invoke(drags[touch.fingerId]);
                        finger.InvokeDragEnded();
                    }
                    StartCoroutine(RemoveAtTheEndOfFrame(finger));
                    break;
            }
        }
    }

    private void OnDisable()
    {
        foreach (var kvp in fingers)
        {
            touchEnded?.Invoke(kvp.Value);
            kvp.Value.InvokeTouchEnded();
            var touch = kvp.Value.touch;
            if (drags.ContainsKey(touch.fingerId))
            {
                dragEnded?.Invoke(drags[touch.fingerId]);
                kvp.Value.InvokeDragEnded();
            }
        }
    }

    // Keep fingers until the end of frame, so that we can work with them in all scripts' Update functions etc.
    private WaitForEndOfFrame wait = new();
    private IEnumerator RemoveAtTheEndOfFrame(Finger finger)
    {
        yield return wait;
        var touch = finger.touch;
        if (drags.ContainsKey(touch.fingerId))
            drags.Remove(touch.fingerId);
        fingers.Remove(touch.fingerId);
    }

    private void HandleDrags(Finger finger)
    {
        if (drags.ContainsKey(finger.touch.fingerId))
        {
            dragged?.Invoke(finger);
            finger.InvokeDragged();
        }
        else if ((finger.touch.position - finger.pressPosition).sqrMagnitude > 100)
        {
            drags[finger.touch.fingerId] = finger;
            dragStarted?.Invoke(finger);
            finger.InvokeDragStarted();
        }
    }
}
