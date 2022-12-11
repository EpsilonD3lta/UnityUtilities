using System;
using System.Collections.Generic;
using UnityEngine;

public class TouchInput : MonoBehaviour
{
    public class TouchData
    {
        public Vector2 initialContactPoint;
        public Touch touch;
    }

    public event Action<TouchData> touchStarted, touchStayed, touchMoved, touchEnded;

    private Dictionary<int, TouchData> activeTouches = new Dictionary<int, TouchData>();

    private void Update()
    {
        for (int i = 0; i < Input.touchCount; i++)
        {
            var touch = Input.GetTouch(i);
            switch (touch.phase)
            {
                case TouchPhase.Began:
                    activeTouches[touch.fingerId] = new TouchData()
                    {
                        initialContactPoint = touch.position,
                        touch = touch
                    };
                    touchStarted?.Invoke(activeTouches[touch.fingerId]);
                    break;
                case TouchPhase.Stationary:
                    activeTouches[touch.fingerId].touch = touch;
                    touchStayed?.Invoke(activeTouches[touch.fingerId]);
                    break;
                case TouchPhase.Moved:
                    activeTouches[touch.fingerId].touch = touch;
                    touchMoved?.Invoke(activeTouches[touch.fingerId]);
                    break;
                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    touchEnded?.Invoke(activeTouches[touch.fingerId]);
                    activeTouches.Remove(touch.fingerId);
                    break;
            }
        }
    }
}
