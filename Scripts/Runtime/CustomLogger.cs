using System;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Reroute UnityEngine.Logger through this custom logger
/// Use at the beginning of game: Debug.unityLogger.logHandler = new CustomLogger();
/// </summary>
public class CustomLogger : ILogHandler
{
    private const string defaultTag = "[XXX]";

    public ILogHandler logHandler
    {
        get;
        set;
    }

    public CustomLogger()
    {
        this.logHandler = Debug.unityLogger.logHandler;
    }

    public void LogException(Exception exception, Object context)
    {
        logHandler.LogException(exception, context);
    }

    public void LogFormat(LogType logType, Object context, string format, params object[] args)
    {
        // Android native log
#if UNITY_ANDROID && !UNITY_EDITOR && false
        var Log = new AndroidJavaClass("android.util.Log");
        switch (logType)
        {
            case LogType.Log:
                Log.CallStatic<int>("v", defaultTag, string.Format(format, args));
                break;
            case LogType.Warning:
                Log.CallStatic<int>("w", defaultTag, string.Format(format, args));
                break;
            default:
                Log.CallStatic<int>("e", defaultTag, string.Format(format, args));
                break;
        }
#else
        logHandler.LogFormat(logType, context, "{0} {1}", defaultTag, string.Format(format, args));
#endif

    }
}
