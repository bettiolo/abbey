using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abbey.EditorTools
{
    /// <summary>
    /// Pure validation logic for imported generated assets, separated from
    /// <see cref="GeneratedAssetImporter"/> (which touches AssetDatabase and the
    /// filesystem) so EditMode tests can run these checks on fake hierarchies and
    /// raw JSON strings with no glTFast import involved.
    /// </summary>
    public static class GeneratedAssetValidator
    {
        // ------------------------------------------------------------------
        // Metadata parsing (blender/generated/metadata/<id>.meta.json)
        // ------------------------------------------------------------------

        [Serializable]
        public class AnchorEntry
        {
            public string name;
            public string type;
        }

        [Serializable]
        class MetaFile
        {
            public string id;
            public AnchorEntry[] anchors;
        }

        /// <summary>
        /// Extracts the top-level anchor names from an asset metadata JSON.
        /// (The pipeline writes anchors both inside "spec" and at the top level;
        /// JsonUtility binds only the declared top-level "anchors" field.)
        /// Returns an empty array when the asset declares no anchors.
        /// </summary>
        public static string[] ParseAnchorNames(string metaJson)
        {
            if (string.IsNullOrEmpty(metaJson))
            {
                return new string[0];
            }
            var meta = JsonUtility.FromJson<MetaFile>(metaJson);
            if (meta?.anchors == null)
            {
                return new string[0];
            }
            var names = new List<string>(meta.anchors.Length);
            foreach (var anchor in meta.anchors)
            {
                if (anchor != null && !string.IsNullOrEmpty(anchor.name))
                {
                    names.Add(anchor.name);
                }
            }
            return names.ToArray();
        }

        // ------------------------------------------------------------------
        // Hierarchy validation
        // ------------------------------------------------------------------

        /// <summary>
        /// Returns every anchor name that has no matching descendant transform
        /// under <paramref name="root"/> (exact name match, any depth).
        /// Empty list = all anchors present.
        /// </summary>
        public static List<string> FindMissingAnchors(
            Transform root, IReadOnlyList<string> anchorNames)
        {
            var missing = new List<string>();
            if (anchorNames == null)
            {
                return missing;
            }
            for (int i = 0; i < anchorNames.Count; i++)
            {
                string name = anchorNames[i];
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }
                if (root == null || FindDescendant(root, name) == null)
                {
                    missing.Add(name);
                }
            }
            return missing;
        }

        /// <summary>Depth-first search for a descendant with the exact name.</summary>
        public static Transform FindDescendant(Transform root, string name)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child.name == name)
                {
                    return child;
                }
                var deeper = FindDescendant(child, name);
                if (deeper != null)
                {
                    return deeper;
                }
            }
            return null;
        }

        // ------------------------------------------------------------------
        // Report shape (serialized to unity/Assets/Generated/import_report.json)
        // ------------------------------------------------------------------

        [Serializable]
        public class AssetResult
        {
            public string id;
            public bool imported;
            public bool passed;
            public int anchorsChecked;
            public string[] missingAnchors;
            public string message;
        }

        [Serializable]
        public class ImportReport
        {
            public string generatedAt;
            public int totalAssets;
            public int passed;
            public int failed;
            public string message;
            public List<AssetResult> results;
        }
    }
}
