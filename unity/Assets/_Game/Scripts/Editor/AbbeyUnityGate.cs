using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
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
        const int EditorReadyTimeoutSeconds = 60;

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
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                    if (!WaitForEditorReady(EditorReadyTimeoutSeconds))
                    {
                        throw new TimeoutException(
                            $"Unity stayed busy compiling/updating for {EditorReadyTimeoutSeconds}s.");
                    }
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
                            report.assetImportValidation == Pass &&
                            report.canonicalScreenshots != null &&
                            report.canonicalScreenshots.Count >=
                            ScreenshotCapture.CanonicalShotNames.Length &&
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
                   UnityEngine.Object.FindFirstObjectByType<PrototypePhase3Bootstrap>() != null;
        }

        static bool WaitForEditorReady(int timeoutSeconds)
        {
            double start = EditorApplication.timeSinceStartup;
            while (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                if (EditorApplication.timeSinceStartup - start > timeoutSeconds)
                {
                    return false;
                }
                Thread.Sleep(100);
            }
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
            public string assetImportValidation;
            public int consoleErrorCount;
            public List<string> canonicalScreenshots;
            public List<string> errors;
            public bool passed;
        }
    }
}
