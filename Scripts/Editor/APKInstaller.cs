using System.Diagnostics;
using UnityEditor;

namespace Tools
{
    public static class APKInstaller
    {
        private static string adbPath = EditorPrefs.GetString("AndroidSdkRoot") + "/platform-tools/adb";
        //This tool can be used as an alternative way to analyze apk file.
        //private static string aaptPath = EditorPrefs.GetString("AndroidSdkRoot") + "/build-tools/*/aapt";
        private static string apkAnalyzerPath = EditorPrefs.GetString("AndroidSdkRoot") + "/cmdline-tools/latest/bin/apkanalyzer.bat";

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

            string result = "Result: " + installProcess.StandardOutput.ReadLine() + ": " + installProcess.StandardOutput.ReadToEnd();
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
            string appIdentifier = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android);
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
            string result = "Running app " + appIdentifier + ": " + runProcess.StandardOutput.ReadLine() + ": " + runProcess.StandardOutput.ReadToEnd();
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
            string result = "Running app (monkey) " + appIdentifier + ": " + runProcess.StandardOutput.ReadLine() + ": " + runProcess.StandardOutput.ReadToEnd();
            result += runProcess.StandardError.ReadToEnd();

            if (runProcess.ExitCode != 0)
                UnityEngine.Debug.LogError(result);
            else
                UnityEngine.Debug.Log(result);
        }
    }
}
