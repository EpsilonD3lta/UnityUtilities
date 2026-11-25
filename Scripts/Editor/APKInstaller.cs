using System.Diagnostics;
using UnityEditor;
#if UNITY_2019_4_OR_NEWER && UNITY_ANDROID
using UnityEditor.Android;
#endif

namespace EpsilonDelta
{
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public static class APKInstaller
    {
        private static string adbPath = EditorPrefs.GetString("AndroidSdkRoot") + "/platform-tools/adb";
        //This tool can be used as an alternative way to analyze apk file.
        //private static readonly string aaptPath = EditorPrefs.GetString("AndroidSdkRoot") + "/build-tools/*/aapt";
        private static readonly string apkAnalyzerPath = EditorPrefs.GetString("AndroidSdkRoot") + "/cmdline-tools/latest/bin/apkanalyzer.bat";

        [MenuItem("File/Install APK", priority = 211)]
        public static void InstallAPK()
        {
            string[] filter = new string[] { "Android build", "apk" };
            string apkPath = EditorUtility.OpenFilePanelWithFilters("Choose .apk File", "", filter);
            if (string.IsNullOrEmpty(apkPath)) return;
            Install(apkPath);
        }

        [MenuItem("File/Install and run APK", priority = 211)]
        public static void InstallAndRunAPK()
        {
            string[] filter = new string[] { "Android build", "apk" };
            string apkPath = EditorUtility.OpenFilePanelWithFilters("Choose .apk File", "", filter);
            if (string.IsNullOrEmpty(apkPath)) return;
            Install(apkPath, true);
        }

        public static void Install(string apkPath, bool run = false)
        {
            // Newly exposed API, only available if Android Module is installed.
            // Points to the SDK in Unity Editor folders. Unity usually uses these SDKs.
            // EditorPrefs.GetString("AndroidSkdRoot") usually points to the same SDKs in newer versions of Unity,
            // in older versions of Unity it usually points to Android studio SDK in Users folder,
            // but you can set your custom SDK in Preferences and EditorPrefs will point there (might be empty).
#if UNITY_2019_4_OR_NEWER && UNITY_ANDROID
            if (string.IsNullOrEmpty(EditorPrefs.GetString("AndroidSdkRoot")))
                adbPath = AndroidExternalToolsSettings.sdkRootPath + "/platform-tools/adb";
#endif
            ProcessStartInfo process = new ProcessStartInfo(adbPath, "install -r \"" + apkPath + "\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                UseShellExecute = false
            };
            var installProcess = Process.Start(process);
            EditorUtility.DisplayProgressBar("Installing APK", "Installing...", 0.5f);
            installProcess.WaitForExit();

            string result = "Result: " + installProcess.StandardOutput.ReadLine();
            string details = installProcess.StandardOutput.ReadToEnd();
            if (!string.IsNullOrEmpty(details)) result += ": " + details;
            result += installProcess.StandardError.ReadToEnd();

            if (installProcess.ExitCode != 0)
                UnityEngine.Debug.LogError(result);
            else
                UnityEngine.Debug.Log(result);
            EditorUtility.ClearProgressBar();
            if (run) Run();

            //Use RunApk(...) instead of Run(), if package name in player settings does not match apk package name
            //You need to have Command line tools installed, see documentation
            //if (run) RunApk(apkPath);
        }

        public static void Run()
        {
#if (UNITY_6000_0_OR_NEWER)
            string appIdentifier = PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android);
#else
            string appIdentifier = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android);
#endif
            //This is the default Unity android launcher, however in my experience, it does not work properly. I use monkey launcher instead.
            //string mainActivity = "com.unity3d.player.UnityPlayerActivity";
            //string adbCommand = "shell am start -a android.intent.action.MAIN -n " + appIdentifier + "/" + mainActivity;
            string adbCommand = "shell monkey -p \"" + appIdentifier + "\" 1";

            var process = new ProcessStartInfo(adbPath, adbCommand)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                UseShellExecute = false
            };
            var runProcess = Process.Start(process);
            string result = "Running app " + appIdentifier + ": " + runProcess.StandardOutput.ReadLine();
            string details = runProcess.StandardOutput.ReadToEnd();
            if (!string.IsNullOrEmpty(details)) result += ": " + details;
            result += runProcess.StandardError.ReadToEnd();

            if (runProcess.ExitCode != 0)
                UnityEngine.Debug.LogError(result);
            else
                UnityEngine.Debug.Log(result);
        }

        /// <summary>
        /// Use this method, if package name in player settings does not match apk package name
        /// You need to have Command line tools installed, see documentation
        /// </summary>
        /// <param name="apkPath"></param>
        public static void RunApk(string apkPath)
        {
            ProcessStartInfo process = new ProcessStartInfo(apkAnalyzerPath, "manifest application-id \"" + apkPath + "\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                UseShellExecute = false
            };
            var findIdentifierProcess = Process.Start(process);
            findIdentifierProcess.WaitForExit();

            string appIdentifier = findIdentifierProcess.StandardOutput.ReadLine();
            //string mainActivity = "com.unity3d.player.UnityPlayerActivity";
            //string adbCommand = "shell am start -a android.intent.action.MAIN -n " + appIdentifier + "/" + mainActivity;
            string adbCommand = "shell monkey -p \"" + appIdentifier + "\" 1";

            process = new ProcessStartInfo(adbPath, adbCommand)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                UseShellExecute = false
            };
            var runProcess = Process.Start(process);
            string result = "Running app (monkey) " + appIdentifier + ": " + runProcess.StandardOutput.ReadLine();
            string details = runProcess.StandardOutput.ReadToEnd();
            if (!string.IsNullOrEmpty(details)) result += ": " + details;
            result += runProcess.StandardError.ReadToEnd();

            if (runProcess.ExitCode != 0)
                UnityEngine.Debug.LogError(result);
            else
                UnityEngine.Debug.Log(result);
        }
    }
}
