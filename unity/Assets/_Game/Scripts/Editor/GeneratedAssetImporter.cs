using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Abbey.EditorTools
{
    /// <summary>
    /// The "import into Unity → run Unity import validation" step of the canonical
    /// asset loop (AGENTS.md). Sync copies every blender/generated/glb/*.glb into
    /// Assets/Generated/BlenderAssets (repo-relative discovery via
    /// Application.dataPath), reimports, then validates each import: the GLB became
    /// a prefab and every anchor named in blender/generated/metadata/&lt;id&gt;.meta.json
    /// exists as a child transform. The result is written to
    /// unity/Assets/Generated/import_report.json. Validate runs the same checks
    /// without copying. Pure validation logic lives in
    /// <see cref="GeneratedAssetValidator"/> so an EditMode test can exercise it on
    /// a fake hierarchy without glTFast.
    /// </summary>
    public static class GeneratedAssetImporter
    {
        public const string TargetFolder = "Assets/Generated/BlenderAssets";
        public const string ReportPath = "Assets/Generated/import_report.json";

        /// <summary>unity/ project root (parent of Assets).</summary>
        static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        /// <summary>Repo root (parent of unity/).</summary>
        static string RepoRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        static string GlbSourceDir => Path.Combine(RepoRoot, "blender", "generated", "glb");
        static string MetadataDir => Path.Combine(RepoRoot, "blender", "generated", "metadata");

        [MenuItem("Tools/Abbey/Sync Generated Assets")]
        public static void SyncGeneratedAssets()
        {
            var report = SyncGeneratedAssetsForGate();
            int copied = CountSourceGlbs();
            if (copied == 0)
            {
                Debug.LogWarning($"[Abbey] No generated GLBs found at {GlbSourceDir}; nothing to sync.");
                return;
            }
            Debug.Log($"[Abbey] Synced {copied} GLB(s) into {TargetFolder}.");
            LogValidationResult(report);
        }

        [MenuItem("Tools/Abbey/Validate Generated Assets")]
        public static void ValidateGeneratedAssets()
        {
            var report = ValidateGeneratedAssetsForGate();
            LogValidationResult(report);
        }

        /// <summary>
        /// Copies committed Blender GLBs into Unity's generated asset folder,
        /// imports them synchronously, and returns the validation report.
        /// </summary>
        public static GeneratedAssetValidator.ImportReport SyncGeneratedAssetsForGate()
        {
            if (!Directory.Exists(GlbSourceDir))
            {
                var missing = new GeneratedAssetValidator.ImportReport
                {
                    generatedAt = DateTime.UtcNow.ToString("o"),
                    message = $"source dir missing: {GlbSourceDir}",
                    results = new List<GeneratedAssetValidator.AssetResult>(),
                };
                WriteReport(missing);
                return missing;
            }

            string targetAbsolute = Path.Combine(ProjectRoot, TargetFolder);
            Directory.CreateDirectory(targetAbsolute);

            foreach (string source in Directory.GetFiles(GlbSourceDir, "*.glb"))
            {
                string destination = Path.Combine(targetAbsolute, Path.GetFileName(source));
                File.Copy(source, destination, overwrite: true);
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            return ValidateGeneratedAssetsForGate();
        }

        static void LogValidationResult(GeneratedAssetValidator.ImportReport report)
        {
            if (report.failed > 0)
            {
                Debug.LogError($"[Abbey] Generated asset validation: {report.failed}/{report.totalAssets} " +
                               $"FAILED (see {ReportPath}).");
            }
            else
            {
                Debug.Log($"[Abbey] Generated asset validation: all {report.totalAssets} passed " +
                          $"(report at {ReportPath}).");
            }
        }

        /// <summary>
        /// Runs the Unity-side generated asset validation and writes the normal
        /// import report, but leaves logging/exit-code decisions to the caller.
        /// Used by the combined Abbey Unity gate.
        /// </summary>
        public static GeneratedAssetValidator.ImportReport ValidateGeneratedAssetsForGate()
        {
            var report = BuildReport();
            WriteReport(report);
            return report;
        }

        static int CountSourceGlbs()
        {
            return Directory.Exists(GlbSourceDir)
                ? Directory.GetFiles(GlbSourceDir, "*.glb").Length
                : 0;
        }

        static GeneratedAssetValidator.ImportReport BuildReport()
        {
            var report = new GeneratedAssetValidator.ImportReport
            {
                generatedAt = DateTime.UtcNow.ToString("o"),
                results = new List<GeneratedAssetValidator.AssetResult>(),
            };

            if (!Directory.Exists(GlbSourceDir))
            {
                report.message = $"source dir missing: {GlbSourceDir}";
                return report;
            }

            foreach (string source in Directory.GetFiles(GlbSourceDir, "*.glb"))
            {
                string id = Path.GetFileNameWithoutExtension(source);
                report.results.Add(ValidateOne(id));
            }

            report.totalAssets = report.results.Count;
            foreach (var result in report.results)
            {
                if (result.passed)
                {
                    report.passed++;
                }
                else
                {
                    report.failed++;
                }
            }
            return report;
        }

        static GeneratedAssetValidator.AssetResult ValidateOne(string id)
        {
            var result = new GeneratedAssetValidator.AssetResult
            {
                id = id,
                missingAnchors = new string[0],
            };

            string assetPath = $"{TargetFolder}/{id}.glb";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            result.imported = prefab != null;
            if (!result.imported)
            {
                result.passed = false;
                result.message = $"not imported at {assetPath} (run Tools/Abbey/Sync Generated Assets)";
                return result;
            }

            string metaPath = Path.Combine(MetadataDir, $"{id}.meta.json");
            if (!File.Exists(metaPath))
            {
                result.passed = false;
                result.message = $"metadata missing: {metaPath}";
                return result;
            }

            string[] anchors;
            try
            {
                anchors = GeneratedAssetValidator.ParseAnchorNames(File.ReadAllText(metaPath));
            }
            catch (Exception e)
            {
                result.passed = false;
                result.message = $"metadata unreadable: {e.Message}";
                return result;
            }

            var missing = GeneratedAssetValidator.FindMissingAnchors(prefab.transform, anchors);
            result.missingAnchors = missing.ToArray();
            result.anchorsChecked = anchors.Length;
            result.passed = missing.Count == 0;
            result.message = result.passed
                ? $"ok ({anchors.Length} anchor(s) present)"
                : $"missing anchor transform(s): {string.Join(", ", missing)}";
            return result;
        }

        static void WriteReport(GeneratedAssetValidator.ImportReport report)
        {
            string absolute = Path.Combine(ProjectRoot, ReportPath);
            if (File.Exists(absolute) && ReportMatchesExceptGeneratedAt(
                    File.ReadAllText(absolute), report))
            {
                return;
            }

            string dir = Path.GetDirectoryName(absolute);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllText(absolute, JsonUtility.ToJson(report, prettyPrint: true) + "\n");
            AssetDatabase.Refresh();
        }

        public static bool ReportMatchesExceptGeneratedAt(
            string existingJson, GeneratedAssetValidator.ImportReport report)
        {
            if (string.IsNullOrEmpty(existingJson) || report == null)
            {
                return false;
            }

            try
            {
                var existing = JsonUtility.FromJson<GeneratedAssetValidator.ImportReport>(
                    existingJson);
                if (existing == null)
                {
                    return false;
                }

                existing.generatedAt = report.generatedAt;
                string existingNormalized =
                    JsonUtility.ToJson(existing, prettyPrint: true) + "\n";
                string current = JsonUtility.ToJson(report, prettyPrint: true) + "\n";
                return existingNormalized == current;
            }
            catch
            {
                return false;
            }
        }
    }
}
