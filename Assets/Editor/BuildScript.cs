using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AeroTerra.EditorTools
{
    /// <summary>
    /// Headless multi-platform builds, invoked by scripts/build-all.sh:
    ///   Unity -batchmode -executeMethod AeroTerra.EditorTools.BuildScript.BuildWindows  (etc.)
    /// </summary>
    public static class BuildScript
    {
        private static string[] Scenes =>
            EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();

        public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, "Builds/Windows/AeroTerra.exe");
        public static void BuildLinux()   => Build(BuildTarget.StandaloneLinux64, "Builds/Linux/AeroTerra.x86_64");
        public static void BuildMac()     => Build(BuildTarget.StandaloneOSX, "Builds/macOS/AeroTerra.app");
        public static void BuildAndroid()
        {
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            EditorUserBuildSettings.buildAppBundle = false;
            Build(BuildTarget.Android, "Builds/Android/AeroTerra.apk");
        }
        public static void BuildIOS() => Build(BuildTarget.iOS, "Builds/iOS"); // Xcode project output

        public static void BuildWebGL()
        {
            // WebGL-specific tuning: smaller memory footprint, compressed
            // delivery, and a template that shows a loading bar while Cesium
            // streams the first tiles.
            PlayerSettings.WebGL.memorySize = 512;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.template = "APPLICATION:Default";
            Build(BuildTarget.WebGL, "Builds/WebGL");
        }

        private static void Build(BuildTarget target, string path)
        {
            PlayerSettings.companyName = "AeroTerra";
            PlayerSettings.productName = "AeroTerra";
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = Scenes, target = target, locationPathName = path,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"Build failed for {target}: {report.summary.result}");
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
            else
            {
                Debug.Log($"Build OK: {target} → {path} ({report.summary.totalSize / (1024 * 1024)} MB)");
            }
        }
    }
}
