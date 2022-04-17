using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class Extensions
{
    #region Navmesh
    public static float GetPathLength(this NavMeshPath navMeshPath)
    {
        float pathLength = 0;
        for (int i = 1; i < navMeshPath.corners.Length; ++i)
        {
            pathLength += (navMeshPath.corners[i] - navMeshPath.corners[i - 1]).magnitude;
        }

        return pathLength;
    }

    public static float CalculatePathLength(this NavMeshAgent navMeshAgent, Vector3 target)
    {
        NavMeshPath path = new NavMeshPath();
        navMeshAgent.CalculatePath(target, path);
        return path.GetPathLength();
    }
    #endregion

    #region Math and vectors
    public static Vector3 XZ(this Vector3 v)
    {
        return new Vector3(v.x, 0, v.z);
    }

    public static Vector3 Y(this Vector3 v)
    {
        return new Vector3(0, v.y, 0);
    }

    public static Vector3 SetX(this Vector3 v, float x)
    {
        return new Vector3(x, v.y, v.z);
    }

    public static Vector3 SetY(this Vector3 v, float y)
    {
        return new Vector3(v.x, y, v.z);
    }

    public static Vector3 SetZ(this Vector3 v, float z)
    {
        return new Vector3(v.x, v.y, z);
    }

    /// <summary>
    /// Modulus that always returns non-negative integer.
    /// </summary>
    public static int Mod(int x, int m)
    {
        return (x % m + m) % m;
    }

    /// <summary>
    /// Modulus (float) that always returns non-negative float.
    /// </summary>
    public static float Mod(float x, float m)
    {
        return (x % m + m) % m;
    }

    public static Vector3 NearestPointOnLine(Vector3 linePoint, Vector3 lineDir, Vector3 point)
    {
        lineDir.Normalize();
        var v = point - linePoint;
        var d = Vector3.Dot(v, lineDir);
        return linePoint + lineDir * d;
    }

    public static float PointToLineDistance(Vector3 linePoint, Vector3 lineDir, Vector3 point)
    {
        lineDir.Normalize();
        var v = point - linePoint;
        var d = Vector3.Dot(v, lineDir);
        return (v - lineDir * d).magnitude;
    }
    #endregion

    #region Colors
    public static Color SetAlpha(this Color c, float a)
    {
        return new Color(c.r, c.g, c.b, a);
    }

    public static Color SetRgb(this Color c, float r, float g, float b)
    {
        return new Color(r, g, b, c.a);
    }

    #endregion

    #region LineRenderer
    public static void AddPosition(this LineRenderer l, Vector3 newPosition)
    {
        l.positionCount++;
        l.SetPosition(l.positionCount - 1, newPosition);
    }

    public static Vector3 GetLastPosition(this LineRenderer l)
    {
        int lastIndex = l.positionCount - 1;
        return l.GetPosition(lastIndex);
    }
    #endregion

    public static string Serialize<T>(this T obj)
    {
        return JsonUtility.ToJson(obj);
    }

    public static bool Contains(this LayerMask layerMask, int layer)
    {
        return layerMask == (layerMask | (1 << layer));
    }

    /// <summary>
    /// This method returns desired component from the topmost parent (or itself) that has the component.
    /// This is the opposite to GetComponentInParent, which returns the first parent (or itself).
    /// However this method searches on inactive gameobjects, while GetComponentInParent does not.
    /// </summary>
    public static T GetComponentInTopParent<T>(this Transform transform)
        where T : Component
    {
        T t0 = transform.GetComponent<T>();
        while (transform.parent != null)
        {
            T t1 = transform.parent.GetComponent<T>();
            if (t1 != null) t0 = t1;

            transform = transform.parent;
        }

        return t0;
    }

    /// <summary>
    /// GetComponentInParent that can search also inactive parents.
    /// </summary>
    public static T GetComponentInParent<T>(this Transform transform, bool includeInactive)
        where T : Component
    {
        if (!includeInactive)
        {
            return transform.GetComponentInParent<T>();
        }
        else
        {
            T t0 = transform.GetComponent<T>();
            while (t0 == null && transform.parent != null)
            {
                t0 = transform.parent.GetComponent<T>();
                transform = transform.parent;
            }

            return t0;
        }
    }

    public static T FindComponentInScene<T>(bool includeInactive = false)
    {
        GameObject[] rootGOs = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        T component;
        foreach (GameObject rootGO in rootGOs)
        {
            component = rootGO.GetComponentInChildren<T>(includeInactive);
            if (component != null)
            {
                return component;
            }
        }
        return default;
    }

    /// <summary>
    /// Finds all components in scene
    /// </summary>
    /// <param name="includeInactive">Same as in GetComponentsInChildren, where default is false</param>
    /// <returns></returns>
    public static List<T> FindComponentsInScene<T>(bool includeInactive = false)
    {
        GameObject[] rootGOs = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        List<T> components = new List<T>();
        foreach (GameObject rootGO in rootGOs)
        {
            var rootGOComponents = rootGO.GetComponentsInChildren<T>(includeInactive);
            if (rootGOComponents.Length > 0) components.AddRange(rootGOComponents);
        }

        return components;
    }

    public static IEnumerator SetActive(this GameObject gameObject, bool active, float delay)
    {
        yield return new WaitForSeconds(delay);
        gameObject.SetActive(active);
    }

    /// <summary>
    /// Check if pointer is over any and only GraphicRaycaster. Unity's EventSystem.current.IsPointerOverGameObject is
    /// checking against 3D or 2D physics when Camera has Physics or Physics2D Raycaster attached.
    /// </summary>
    /// <returns></returns>
    public static bool IsPointerOverUI()
    {
        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition,
        };
        GraphicRaycaster[] graphicRaycasters = GameObject.FindObjectsOfType<GraphicRaycaster>();
        List<RaycastResult> results = new List<RaycastResult>();
        foreach (GraphicRaycaster gr in graphicRaycasters)
        {
            if (gr) gr.Raycast(pointerData, results);
            if (results.Count > 0) return true;
        }

        return false;
    }

    #region Animation
    /// <summary>
    /// Returns the length of the animation, in seconds
    /// </summary>
    public static float GetDuration(this AnimationCurve curve)
    {
        return curve.length > 0 ? curve.keys[curve.length - 1].time : 0;
    }

    /// <summary>
    /// Returns the value of the animation at its end
    /// </summary>
    public static float GetFinalValue(this AnimationCurve curve)
    {
        return curve.length > 0 ? curve.keys[curve.length - 1].value : -1;
    }
    #endregion

    #region Audio
    /// <summary>
    /// Stops audiosource only if it is playing given audioclip.
    /// </summary>
    /// <param name="stopIfNull">Stop audiosource if provided clip is null.</param>
    public static void Stop(this AudioSource audioSource, AudioClip audioClip, bool removeClip = false, bool stopIfNull = false)
    {
        if (audioSource.clip == audioClip)
        {
            audioSource.Stop();
            if (removeClip) audioSource.clip = null;
        }
        else if (audioClip == null && stopIfNull) audioSource.Stop();
    }

    /// <summary>
    /// Pause audiosource only if it is playing given audioclip.
    /// </summary>
    /// <param name="stopIfNull">Pause audiosource if provided clip is null.</param>
    public static void Pause(this AudioSource audioSource, AudioClip audioClip, bool pauseIfNull = false)
    {
        if (audioSource.clip == audioClip) audioSource.Pause();
        else if (audioClip == null && pauseIfNull) audioSource.Pause();
    }

    /// <summary>
    /// Replaces current audioclip in audiosource and plays it. Paused audioSource have isPlaying set to false.
    /// Paused audioSource will always only resume regardless of forceRestart.
    /// </summary>
    /// <param name="forceRestart">If the same clip is playing, should it play from the start?</param>
    /// <param name="forceClip">If different clip is playing, should it play the new one?</param>
    /// <param name="playIfNull">Play audioSource if provided clip is null.</param>
    public static void Play(this AudioSource audioSource, AudioClip audioClip, bool forceRestart = true, bool forceClip = true, bool playIfNull = false)
    {
        if (audioClip == null)
        {
            if (!playIfNull) return;
        }
        else
        {
            if (!audioSource.isPlaying) audioSource.clip = audioClip;
            else if (forceClip) audioSource.clip = audioClip;
        }

        if (!audioSource.isPlaying) audioSource.Play();
        else
        {
            if (forceClip)
            {
                if (forceRestart) audioSource.Play();
                else if (audioSource.clip != audioClip) audioSource.Play();
            }
            else
            {
                if (forceRestart)
                {
                    if (audioSource.clip == audioClip) audioSource.Play();
                }
            }
        }
    }
    #endregion
}

public class Debug : UnityEngine.Debug
{
    private static Color green = Color.green;
    private static Color yellow = Color.yellow;
    private static Color red = Color.red;

    public static void LogGreen(object message)
    {
        string hexGreen = ColorUtility.ToHtmlStringRGBA(green);
        Debug.Log($"<color=#{hexGreen}>{message}</color>");
    }

    public static void LogGreen(object message, Object context)
    {
        string hexGreen = ColorUtility.ToHtmlStringRGBA(green);
        Debug.Log($"<color=#{hexGreen}>{message}</color>", context);
    }

    public static void LogYellow(object message)
    {
        string hexYellow = ColorUtility.ToHtmlStringRGBA(yellow);
        Debug.Log($"<color=#{hexYellow}>{message}</color>");
    }

    public static void LogYellow(object message, Object context)
    {
        string hexYellow = ColorUtility.ToHtmlStringRGBA(yellow);
        Debug.Log($"<color=#{hexYellow}>{message}</color>", context);
    }

    public static void LogRed(object message)
    {
        string hexRed = ColorUtility.ToHtmlStringRGBA(red);
        Debug.Log($"<color=#{hexRed}>{message}</color>");
    }

    public static void LogRed(object message, Object context)
    {
        string hexRed = ColorUtility.ToHtmlStringRGBA(red);
        Debug.Log($"<color=#{hexRed}>{message}</color>", context);
    }
}
