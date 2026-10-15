using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Headless Android build: unity build <project> --target Android --execute-method AndroidBuilder.Build --output-path Builds/ZombiesMobile.apk
public static class AndroidBuilder
{
    const string Scene = "Assets/Scenes/ZombiesTestLevel.unity";

    [MenuItem("Zombies/Build Android APK")]
    public static void BuildFromMenu() => Run("Builds/ZombiesMobile.apk");

    public static void Build()
    {
        string output = "Builds/ZombiesMobile.apk";
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == "-buildOutput") output = args[i + 1];

        bool ok = Run(output);
        if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
    }

    static bool Run(string output)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
        EditorUserBuildSettings.buildAppBundle = false;

        var options = new BuildPlayerOptions
        {
            scenes = new[] { Scene },
            locationPathName = output,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.None,
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary s = report.summary;
        Debug.Log($"[AndroidBuilder] {s.result}: {s.outputPath} ({s.totalSize / (1024 * 1024)} MB, {s.totalErrors} errors, {s.totalTime})");
        return s.result == BuildResult.Succeeded;
    }
}
