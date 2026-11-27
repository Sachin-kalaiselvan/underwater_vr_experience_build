using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Abyss.EditorTools
{
    // command line:
    //   Unity -batchmode -quit -projectPath . -buildTarget Android -executeMethod Abyss.EditorTools.AbyssBuild.BuildQuestApk [-abyssDev]
    // -abyssDev makes a development build so the profiler counters (draw calls etc) work in the perf log
    public static class AbyssBuild
    {
        [MenuItem("ABYSS/4. Build Quest APK", priority = 40)]
        public static void BuildFromMenu() => Build(EditorUserBuildSettings.development);

        public static void BuildQuestApk()
        {
            bool dev = System.Environment.GetCommandLineArgs().Contains("-abyssDev");
            var ok = Build(dev);
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        static bool Build(bool dev)
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0) { Debug.LogError("[ABYSS] no scenes in build settings, run ABYSS > 2 first"); return false; }

            var opts = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = $"Builds/ABYSS{(dev ? "_dev" : "")}.apk",
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = dev ? BuildOptions.Development | BuildOptions.ConnectWithProfiler : BuildOptions.None
            };
            EditorUserBuildSettings.buildAppBundle = false;
            var report = BuildPipeline.BuildPlayer(opts);
            var s = report.summary;
            Debug.Log($"[ABYSS] build {s.result}: {s.outputPath} {s.totalSize / (1024f * 1024f):0.0} MB, {s.totalErrors} errors, {s.totalTime}");
            return s.result == BuildResult.Succeeded;
        }
    }
}
