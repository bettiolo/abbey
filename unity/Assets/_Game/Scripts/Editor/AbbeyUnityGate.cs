using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Abbey.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Abbey.EditorTools
{
    /// <summary>
    /// One Unity-side verification gate for humans, MCP, and CI. It composes the
    /// existing editor surfaces instead of duplicating them: generated scene build,
    /// generated asset import validation, console-error check, canonical screenshots,
    /// and a machine-readable report under unity/Build/reports/.
    /// </summary>
    public static class AbbeyUnityGate
    {
        public const string ReportPath = "Build/reports/unity_gate_report.json";
        const string Pass = "pass";
        const string Fail = "fail";
        const string NotRun = "not_run";
        static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        static string AbsoluteReportPath => Path.Combine(ProjectRoot, ReportPath);

        [MenuItem("Tools/Abbey/Run Unity Gate")]
        public static void RunFromMenu()
        {
            var report = RunGate();
            if (report.passed)
            {
                Debug.Log($"[Abbey] Unity gate passed. Report: {ReportPath}");
            }
            else
            {
                Debug.LogError($"[Abbey] Unity gate failed. Report: {ReportPath}");
            }
        }

        /// <summary>
        /// CLI entry:
        /// Unity -batchmode -projectPath unity
        /// -executeMethod Abbey.EditorTools.AbbeyUnityGate.RunFromCLI -quit -logFile -
        /// </summary>
        public static void RunFromCLI()
        {
            int exitCode = 0;
            try
            {
                var report = RunGate();
                exitCode = report.passed ? 0 : 1;
            }
            catch (Exception e)
            {
                var report = CreateReport(DateTime.UtcNow.ToString("o"), Application.unityVersion);
                AddError(report, $"Gate crashed: {e}");
                FinalizeReport(report);
                WriteReport(report);
                Debug.LogError($"[Abbey] Unity gate crashed. Report: {ReportPath}\n{e}");
                exitCode = 1;
            }

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(exitCode);
            }
        }

        public static GateReport RunGate()
        {
            ClearConsoleIfAvailable();
            var collectedLogs = new List<string>();
            Application.logMessageReceived += CollectProblemLog;

            var report = CreateReport(DateTime.UtcNow.ToString("o"), Application.unityVersion);
            try
            {
                RunStep(report, "scene", () =>
                {
                    PrototypeSceneBuilder.BuildPrototypeScene();
                    report.sceneBuilt = ValidatePrototypeScene();
                    if (!report.sceneBuilt)
                    {
                        throw new InvalidOperationException(
                            "Prototype scene did not contain the expected GameClock after build.");
                    }
                });

                RunStep(report, "assetImportValidation", () =>
                {
                    var importReport = GeneratedAssetImporter.SyncGeneratedAssetsForGate();
                    bool passed = importReport.failed == 0 &&
                                  importReport.totalAssets > 0 &&
                                  string.IsNullOrEmpty(importReport.message);
                    report.assetImportValidation = passed ? Pass : Fail;
                    if (!passed)
                    {
                        throw new InvalidOperationException(
                            $"Generated asset validation failed: {importReport.failed}/" +
                            $"{importReport.totalAssets} failed. {importReport.message}");
                    }
                });

                RunStep(report, "compileAndConsoleCheck", () =>
                {
                    // The MCP caller waits for a ready editor before invoking this menu.
                    // Forcing a script Refresh here can request a domain reload, while
                    // synchronously spinning on the editor's main thread prevents that
                    // reload from ever completing. Generated-asset sync above already uses
                    // synchronous imports, so this step only asserts readiness; compiler
                    // diagnostics are collected by the log/console checks below.
                    if (EditorApplication.isCompiling)
                        throw new InvalidOperationException(
                            "Unity gate was invoked while the editor was compiling; retry when ready.");
                });

                if (report.sceneBuilt)
                {
                    RunStep(report, "canonicalScreenshots", () =>
                    {
                        report.canonicalScreenshots =
                            new List<string>(ScreenshotCapture.CaptureCanonicalShotsForGate());
                        if (report.canonicalScreenshots.Count == 0)
                        {
                            throw new InvalidOperationException("No canonical screenshots were captured.");
                        }
                    });
                }
                else
                {
                    AddError(report, "canonicalScreenshots skipped because scene build failed.");
                }

                RunStep(report, "map2Scene", () =>
                {
                    Map2SceneBuilder.BuildMap2Scene();
                    report.map2SceneBuilt = ValidateMap2Scene();
                    if (!report.map2SceneBuilt)
                    {
                        throw new InvalidOperationException(
                            "Map 2 scene did not contain its Stag, scenario, and generated landmarks.");
                    }
                });

                if (report.map2SceneBuilt)
                {
                    RunStep(report, "map2Screenshots", () =>
                    {
                        report.map2Screenshots =
                            new List<string>(Map2ScreenshotCapture.CaptureForGate());
                        if (report.map2Screenshots.Count < Map2ScreenshotCapture.ShotNames.Length)
                            throw new InvalidOperationException("Map 2 proof screenshots were not captured.");
                    });
                }
                else
                {
                    AddError(report, "map2Screenshots skipped because Map 2 scene build failed.");
                }
            }
            finally
            {
                Application.logMessageReceived -= CollectProblemLog;
                for (int i = 0; i < collectedLogs.Count; i++)
                {
                    AddError(report, collectedLogs[i]);
                }

                int consoleErrorCount = GetConsoleErrorCountIfAvailable();
                report.consoleErrorCount = consoleErrorCount;
                if (consoleErrorCount > 0)
                {
                    AddError(report, $"Unity console contains {consoleErrorCount} error(s).");
                }

                FinalizeReport(report);
                WriteReport(report);
            }

            return report;

            void CollectProblemLog(string condition, string stackTrace, LogType type)
            {
                if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)
                {
                    return;
                }

                string message = string.IsNullOrEmpty(stackTrace)
                    ? condition
                    : $"{condition}\n{stackTrace}";
                collectedLogs.Add(message);
            }
        }

        public static GateReport CreateReport(string generatedAt, string unityVersion)
        {
            return new GateReport
            {
                generatedAt = generatedAt,
                unityVersion = unityVersion,
                assetImportValidation = NotRun,
                canonicalScreenshots = new List<string>(),
                map2Screenshots = new List<string>(),
                errors = new List<string>(),
            };
        }

        public static void AddError(GateReport report, string message)
        {
            if (report == null || string.IsNullOrEmpty(message))
            {
                return;
            }
            if (!report.errors.Contains(message))
            {
                report.errors.Add(message);
            }
        }

        public static void FinalizeReport(GateReport report)
        {
            if (report == null)
            {
                return;
            }

            report.passed = report.sceneBuilt &&
                            report.map2SceneBuilt &&
                            report.assetImportValidation == Pass &&
                            report.canonicalScreenshots != null &&
                            report.canonicalScreenshots.Count >=
                            ScreenshotCapture.CanonicalShotNames.Length &&
                            report.map2Screenshots != null &&
                            report.map2Screenshots.Count >= Map2ScreenshotCapture.ShotNames.Length &&
                            report.errors != null &&
                            report.errors.Count == 0;
        }

        static void RunStep(GateReport report, string stepName, Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                AddError(report, $"{stepName}: {e.Message}");
            }
        }

        static bool ValidatePrototypeScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                return false;
            }

            string absolute = Path.GetFullPath(Path.Combine(ProjectRoot, PrototypeSceneBuilder.ScenePath));
            return File.Exists(absolute) &&
                   UnityEngine.Object.FindFirstObjectByType<GameClock>() != null &&
                   UnityEngine.Object.FindFirstObjectByType<PrototypePhase3Bootstrap>() != null &&
                   UnityEngine.Object.FindFirstObjectByType<Abbey.Nightmares.FalseGuidanceSystem>() != null;
        }

        public static bool ValidateMap2Scene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid()) return false;
            string absolute = Path.GetFullPath(Path.Combine(ProjectRoot, Map2SceneBuilder.ScenePath));
            string[] landmarks =
            {
                "Map2_SacredGrove", "Map2_Orchard", "Map2_DeepForest", "Map2_Stream",
                "Map2_CharcoalCamp", "Map2_DeerPaths", "Map2_StoneCircle",
                "Map2_HiddenGraves", "Map2_CorruptedLoggingCamp", "Map2_AbbeyOfAntlers",
            };
            if (!File.Exists(absolute)
                || UnityEngine.Object.FindFirstObjectByType<Abbey.Map2.Map2Scenario>() == null
                || UnityEngine.Object.FindFirstObjectByType<Abbey.Map2.StagCovenantSystem>() == null
                || GameObject.Find("StagBeneathAbbey") == null)
                return false;
            for (int i = 0; i < landmarks.Length; i++)
                if (GameObject.Find(landmarks[i]) == null) return false;
            return true;
        }

        static void WriteReport(GateReport report)
        {
            string dir = Path.GetDirectoryName(AbsoluteReportPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllText(AbsoluteReportPath, JsonUtility.ToJson(report, prettyPrint: true) + "\n");
        }

        static void ClearConsoleIfAvailable()
        {
            try
            {
                var type = Type.GetType("UnityEditor.LogEntries,UnityEditor");
                var method = type?.GetMethod("Clear", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                method?.Invoke(null, null);
            }
            catch
            {
                // Unity's console API is internal; if it moves, the gate still runs.
            }
        }

        static int GetConsoleErrorCountIfAvailable()
        {
            try
            {
                var type = Type.GetType("UnityEditor.LogEntries,UnityEditor");
                var method = type?.GetMethod(
                    "GetCountsByType",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (method == null)
                {
                    return 0;
                }

                var args = new object[] { 0, 0, 0 };
                method.Invoke(null, args);
                return args[0] is int count ? count : 0;
            }
            catch
            {
                return 0;
            }
        }

        [Serializable]
        public class GateReport
        {
            public string generatedAt;
            public string unityVersion;
            public bool sceneBuilt;
            public bool map2SceneBuilt;
            public string assetImportValidation;
            public int consoleErrorCount;
            public List<string> canonicalScreenshots;
            public List<string> map2Screenshots;
            public List<string> errors;
            public bool passed;
        }
    }
}
