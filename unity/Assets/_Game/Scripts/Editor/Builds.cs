using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Abbey.EditorTools
{
    /// <summary>
    /// Standalone player builds (P01-13). macOS is the first supported target:
    /// StandaloneOSX, output unity/Build/macOS/Abbey.app, scene list = the
    /// bootstrapped Prototype01 scene (built on demand — no .unity file ever needs
    /// to be committed). Batchmode-safe: the CLI entry propagates success/failure
    /// through EditorApplication.Exit so tools/build_macos.sh can gate on it.
    /// </summary>
    public static class Builds
    {
        public const string MacOutputPath = "Build/macOS/Abbey.app";

        [MenuItem("Tools/Abbey/Build macOS App")]
        public static void BuildMacOSFromMenu()
        {
            bool ok = BuildMacOSPlayer();
            Debug.Log(ok ? "[Abbey] macOS build succeeded." : "[Abbey] macOS build FAILED.");
        }

        /// <summary>
        /// CLI entry: Unity -batchmode -quit -projectPath unity
        /// -executeMethod Abbey.EditorTools.Builds.BuildMacOS -logFile -
        /// (also reachable as Abbey.Editor.Builds.BuildMacOS via the legacy shim).
        /// </summary>
        public static void BuildMacOS()
        {
            int exitCode;
            try
            {
                exitCode = BuildMacOSPlayer() ? 0 : 1;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Abbey] macOS build threw: {e}");
                exitCode = 1;
            }

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(exitCode);
            }
        }

        static bool BuildMacOSPlayer()
        {
            // The scene is generated, never hand-authored: build it when missing.
            string sceneAbsolute = Path.GetFullPath(Path.Combine(
                Application.dataPath, "..", PrototypeSceneBuilder.ScenePath));
            if (!File.Exists(sceneAbsolute))
            {
                Debug.Log("[Abbey] Prototype01.unity missing — bootstrapping it first.");
                PrototypeSceneBuilder.BuildPrototypeScene();
                if (!File.Exists(sceneAbsolute))
                {
                    Debug.LogError("[Abbey] Scene bootstrap did not produce " +
                                   $"{PrototypeSceneBuilder.ScenePath}; cannot build.");
                    return false;
                }
            }

            var options = new BuildPlayerOptions
            {
                scenes = new[] { PrototypeSceneBuilder.ScenePath },
                locationPathName = MacOutputPath, // relative to the project root (unity/)
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            Debug.Log($"[Abbey] Build result={summary.result} " +
                      $"size={summary.totalSize} bytes errors={summary.totalErrors} " +
                      $"warnings={summary.totalWarnings} output={summary.outputPath}");
            return summary.result == BuildResult.Succeeded;
        }
    }
}
