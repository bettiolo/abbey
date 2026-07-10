using System;
using System.Collections.Generic;
using System.IO;
using Abbey.Rendering;
using UnityEditor;
using UnityEngine;

namespace Abbey.Editor
{
    /// <summary>Validation gate for manifest geometry, texture imports, and generated catalog drift.</summary>
    public static class MiniWorldProjectionValidator
    {
        [MenuItem("Tools/Abbey/Sprites/Validate Mini World Projection")]
        public static void ValidateFromMenu()
        {
            MiniWorldManifest manifest = MiniWorldSpriteImporter.LoadManifest();
            ThrowIfManifestInvalid(manifest);
            ThrowIfProjectInvalid(manifest);
            Debug.Log(
                $"Mini World projection validation passed: {manifest.files.Length} sheets, " +
                $"{manifest.entries.Length} catalog mappings.");
        }

        public static void ThrowIfManifestInvalid(MiniWorldManifest manifest)
        {
            List<string> errors = CollectManifestErrors(manifest);
            ThrowIfAny(errors, "Mini World manifest validation failed");
        }

        public static void ThrowIfProjectInvalid(MiniWorldManifest manifest)
        {
            List<string> errors = CollectProjectErrors(manifest);
            ThrowIfAny(errors, "Mini World project validation failed");
        }

        public static List<string> CollectManifestErrors(MiniWorldManifest manifest)
        {
            var errors = new List<string>();
            if (manifest == null)
            {
                errors.Add("Manifest is null.");
                return errors;
            }
            if (manifest.schemaVersion != 1)
            {
                errors.Add($"Unsupported schemaVersion {manifest.schemaVersion}; expected 1.");
            }
            if (!string.Equals(manifest.rectOrigin, "bottom-left", StringComparison.Ordinal))
            {
                errors.Add("rectOrigin must be 'bottom-left'.");
            }
            if (manifest.files == null || manifest.files.Length == 0)
            {
                errors.Add("Manifest has no files.");
                return errors;
            }
            if (manifest.entries == null || manifest.entries.Length == 0)
            {
                errors.Add("Manifest has no entries.");
                return errors;
            }

            var fileIds = new HashSet<string>(StringComparer.Ordinal);
            var paths = new HashSet<string>(StringComparer.Ordinal);
            var spriteKeys = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < manifest.files.Length; i++)
            {
                MiniWorldFile file = manifest.files[i];
                string label = file == null ? $"files[{i}]" : file.fileId;
                if (file == null)
                {
                    errors.Add($"files[{i}] is null.");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(file.fileId) || !fileIds.Add(file.fileId))
                {
                    errors.Add($"Duplicate or empty fileId '{file.fileId}'.");
                }
                string assetPath = MiniWorldSpriteImporter.ToAssetPath(file.abbeyPath);
                if (!assetPath.StartsWith(MiniWorldSpriteImporter.CuratedAssetRoot, StringComparison.Ordinal)
                    || !assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add($"{label}: abbeyPath is outside the curated PNG root.");
                }
                if (!paths.Add(assetPath))
                {
                    errors.Add($"Duplicate Abbey path '{assetPath}'.");
                }
                if (!File.Exists(Path.GetFullPath(assetPath)))
                {
                    errors.Add($"{label}: PNG does not exist at {assetPath}.");
                }
                ValidateFileGeometry(file, spriteKeys, errors);
            }

            var assetIds = new HashSet<string>(StringComparer.Ordinal);
            var assetRolePairs = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < manifest.entries.Length; i++)
            {
                MiniWorldEntry entry = manifest.entries[i];
                string label = entry == null ? $"entries[{i}]" : entry.assetId;
                if (entry == null)
                {
                    errors.Add($"entries[{i}] is null.");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(entry.assetId) || !assetIds.Add(entry.assetId))
                {
                    errors.Add($"Duplicate or empty assetId '{entry.assetId}'.");
                }
                if (entry.roles == null || entry.roles.Length != 1
                    || string.IsNullOrWhiteSpace(entry.roles[0]))
                {
                    errors.Add($"{label}: exactly one non-empty role is required.");
                }
                else if (!assetRolePairs.Add(entry.assetId + "\n" + entry.roles[0]))
                {
                    errors.Add($"Duplicate asset/role pair '{entry.assetId}' / '{entry.roles[0]}'.");
                }
                if (!spriteKeys.Contains(entry.defaultSprite))
                {
                    errors.Add($"{label}: defaultSprite '{entry.defaultSprite}' is not a declared slice.");
                }
                if (!(entry.visualScale > 0f) || float.IsNaN(entry.visualScale))
                {
                    errors.Add($"{label}: visualScale must be positive.");
                }
                if (entry.anchorOffset == null || entry.anchorOffset.Length != 2)
                {
                    errors.Add($"{label}: anchorOffset must contain exactly two values.");
                }
                bool hasFootprint = entry.authoredFootprint != null && entry.authoredFootprint.Length > 0;
                if (hasFootprint
                    && (entry.authoredFootprint.Length != 2
                        || entry.authoredFootprint[0] <= 0f
                        || entry.authoredFootprint[1] <= 0f))
                {
                    errors.Add($"{label}: authoredFootprint must contain two positive values or be null.");
                }
                if (RequiresAuthoredFootprint(entry)
                    && (!hasFootprint || entry.authoredFootprint.Length != 2))
                {
                    errors.Add($"{label}: wall/tower mappings require an authored footprint.");
                }
            }
            return errors;
        }

        public static List<string> CollectProjectErrors(MiniWorldManifest manifest)
        {
            var errors = CollectManifestErrors(manifest);
            if (errors.Count > 0)
            {
                return errors;
            }

            var spritesByKey = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            for (int i = 0; i < manifest.files.Length; i++)
            {
                MiniWorldFile file = manifest.files[i];
                string assetPath = MiniWorldSpriteImporter.ToAssetPath(file.abbeyPath);
                TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null)
                {
                    errors.Add($"{file.fileId}: missing TextureImporter.");
                    continue;
                }
                ValidateImportSettings(file, importer, errors);
                IndexImportedSprites(file, assetPath, spritesByKey, errors);
            }

            SpriteProjectionCatalog catalog = AssetDatabase.LoadAssetAtPath<SpriteProjectionCatalog>(
                MiniWorldSpriteImporter.CatalogAssetPath);
            if (catalog == null)
            {
                errors.Add("Generated SpriteProjectionCatalog is missing.");
                return errors;
            }
            ValidateCatalog(manifest, catalog, spritesByKey, errors);
            return errors;
        }

        static void ValidateFileGeometry(
            MiniWorldFile file,
            HashSet<string> spriteKeys,
            List<string> errors)
        {
            string label = file.fileId;
            if (file.dimensions == null || file.expectedDimensions == null
                || file.dimensions.width <= 0 || file.dimensions.height <= 0
                || file.dimensions.width != file.expectedDimensions.width
                || file.dimensions.height != file.expectedDimensions.height)
            {
                errors.Add($"{label}: dimensions and expectedDimensions must match and be positive.");
                return;
            }
            if (file.sheetCellSize == null || file.sheetCellSize.width <= 0 || file.sheetCellSize.height <= 0)
            {
                errors.Add($"{label}: sheetCellSize must be positive.");
                return;
            }
            if (!string.Equals(file.importMode, "multiple", StringComparison.Ordinal))
            {
                errors.Add($"{label}: curated sheets must use multiple import mode.");
            }
            if (file.pixelsPerUnit != 16)
            {
                errors.Add($"{label}: pixelsPerUnit must be 16.");
            }
            if (file.pivot == null || file.pivot.Length != 2)
            {
                errors.Add($"{label}: pivot must contain exactly two values.");
            }
            else
            {
                bool isTileOrUi = string.Equals(file.orientation, "xzTile", StringComparison.Ordinal)
                    || string.Equals(file.category, "ui", StringComparison.Ordinal);
                Vector2 expected = isTileOrUi ? new Vector2(0.5f, 0.5f) : new Vector2(0.5f, 0f);
                if (file.Pivot != expected)
                {
                    errors.Add($"{label}: pivot {file.Pivot} does not match orientation {file.orientation}.");
                }
            }
            if (file.slices == null || file.slices.Length == 0)
            {
                errors.Add($"{label}: no slices declared.");
                return;
            }

            var names = new HashSet<string>(StringComparer.Ordinal);
            var rects = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < file.slices.Length; i++)
            {
                MiniWorldSlice slice = file.slices[i];
                if (slice == null || slice.rect == null)
                {
                    errors.Add($"{label}: slice {i} is null or has no rect.");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(slice.name) || !names.Add(slice.name))
                {
                    errors.Add($"{label}: duplicate or empty slice name '{slice.name}'.");
                }
                string rectKey = $"{slice.rect.x}:{slice.rect.y}:{slice.rect.width}:{slice.rect.height}";
                if (!rects.Add(rectKey))
                {
                    errors.Add($"{label}: duplicate rect {rectKey}.");
                }
                if (slice.rect.width <= 0 || slice.rect.height <= 0
                    || slice.rect.x < 0 || slice.rect.y < 0
                    || slice.rect.x + slice.rect.width > file.dimensions.width
                    || slice.rect.y + slice.rect.height > file.dimensions.height)
                {
                    errors.Add($"{label}:{slice.name}: rect is out of bounds.");
                }
                if (slice.rect.x % file.sheetCellSize.width != 0
                    || slice.rect.y % file.sheetCellSize.height != 0
                    || slice.rect.width % file.sheetCellSize.width != 0
                    || slice.rect.height % file.sheetCellSize.height != 0)
                {
                    errors.Add($"{label}:{slice.name}: rect is not grid-aligned.");
                }
                spriteKeys.Add(file.fileId + ":" + slice.name);
            }
        }

        static void ValidateImportSettings(
            MiniWorldFile file,
            TextureImporter importer,
            List<string> errors)
        {
            string label = file.fileId;
            if (importer.textureType != TextureImporterType.Sprite) errors.Add($"{label}: textureType drift.");
            if (importer.spriteImportMode != SpriteImportMode.Multiple) errors.Add($"{label}: spriteImportMode drift.");
            if (!Mathf.Approximately(importer.spritePixelsPerUnit, file.pixelsPerUnit)) errors.Add($"{label}: PPU drift.");
            if (importer.filterMode != FilterMode.Point) errors.Add($"{label}: filter mode drift.");
            if (importer.mipmapEnabled) errors.Add($"{label}: mipmaps must be disabled.");
            if (importer.textureCompression != TextureImporterCompression.Uncompressed) errors.Add($"{label}: compression drift.");
            if (importer.npotScale != TextureImporterNPOTScale.None) errors.Add($"{label}: NPOT scaling drift.");
            if (!importer.sRGBTexture) errors.Add($"{label}: sRGB must be enabled.");
            if (!importer.alphaIsTransparency) errors.Add($"{label}: alpha transparency must be enabled.");
            if (importer.wrapMode != TextureWrapMode.Clamp) errors.Add($"{label}: wrap mode drift.");
            if (importer.isReadable) errors.Add($"{label}: read/write must be disabled.");
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            if (settings.spriteMeshType != SpriteMeshType.FullRect) errors.Add($"{label}: sprite mesh must be FullRect.");
            if (settings.spriteGenerateFallbackPhysicsShape) errors.Add($"{label}: physics fallback shape must be disabled.");

            SpriteMetaData[] actual = importer.spritesheet;
            if (actual.Length != file.slices.Length)
            {
                errors.Add($"{label}: slice count drift ({actual.Length} != {file.slices.Length}).");
                return;
            }
            for (int i = 0; i < file.slices.Length; i++)
            {
                MiniWorldSlice expected = file.slices[i];
                SpriteMetaData found = actual[i];
                if (!string.Equals(found.name, expected.name, StringComparison.Ordinal)
                    || found.rect != expected.rect.Rect
                    || found.alignment != (int)SpriteAlignment.Custom
                    || found.pivot != file.Pivot
                    || found.border != Vector4.zero)
                {
                    errors.Add($"{label}:{expected.name}: slice metadata drift.");
                }
            }
        }

        static void IndexImportedSprites(
            MiniWorldFile file,
            string assetPath,
            Dictionary<string, Sprite> spritesByKey,
            List<string> errors)
        {
            var expectedNames = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < file.slices.Length; i++) expectedNames.Add(file.slices[i].name);
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            for (int i = 0; i < assets.Length; i++)
            {
                if (!(assets[i] is Sprite sprite) || !expectedNames.Contains(sprite.name)) continue;
                string key = file.fileId + ":" + sprite.name;
                if (spritesByKey.ContainsKey(key)) errors.Add($"{key}: imported more than once.");
                else spritesByKey.Add(key, sprite);
            }
            foreach (string name in expectedNames)
            {
                string key = file.fileId + ":" + name;
                if (!spritesByKey.ContainsKey(key)) errors.Add($"{key}: imported sprite is missing.");
            }
        }

        static void ValidateCatalog(
            MiniWorldManifest manifest,
            SpriteProjectionCatalog catalog,
            Dictionary<string, Sprite> spritesByKey,
            List<string> errors)
        {
            if (catalog.entries == null || catalog.entries.Count != manifest.entries.Length)
            {
                errors.Add("Catalog entry count differs from the manifest.");
                return;
            }
            var assetIds = new HashSet<string>(StringComparer.Ordinal);
            var assetRolePairs = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < manifest.entries.Length; i++)
            {
                MiniWorldEntry expected = manifest.entries[i];
                SpriteProjectionEntry actual = catalog.entries[i];
                if (actual == null)
                {
                    errors.Add($"Catalog entry {i} is null.");
                    continue;
                }
                if (!assetIds.Add(actual.assetId)) errors.Add($"Catalog duplicates assetId '{actual.assetId}'.");
                if (!assetRolePairs.Add(actual.assetId + "\n" + actual.role))
                    errors.Add($"Catalog duplicates asset/role pair '{actual.assetId}' / '{actual.role}'.");
                spritesByKey.TryGetValue(expected.defaultSprite, out Sprite expectedSprite);
                if (!string.Equals(actual.assetId, expected.assetId, StringComparison.Ordinal)
                    || !string.Equals(actual.role, expected.roles[0], StringComparison.Ordinal)
                    || actual.sprite == null || actual.sprite != expectedSprite
                    || !Mathf.Approximately(actual.visualScale, expected.visualScale)
                    || actual.anchorOffset != expected.AnchorOffset
                    || actual.sortingOffset != expected.roleSortOffset
                    || actual.authoredFootprint != expected.AuthoredFootprint)
                {
                    errors.Add($"Catalog mapping drift at index {i} ({expected.assetId}).");
                }
            }
        }

        static bool RequiresAuthoredFootprint(MiniWorldEntry entry)
        {
            string role = entry.roles != null && entry.roles.Length > 0 ? entry.roles[0] : string.Empty;
            return ContainsWallOrTower(entry.assetId) || ContainsWallOrTower(role);
        }

        static bool ContainsWallOrTower(string value)
        {
            return !string.IsNullOrEmpty(value)
                && (value.IndexOf("wall", StringComparison.OrdinalIgnoreCase) >= 0
                    || value.IndexOf("tower", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        static void ThrowIfAny(List<string> errors, string heading)
        {
            if (errors.Count == 0) return;
            throw new InvalidOperationException(heading + ":\n - " + string.Join("\n - ", errors));
        }
    }
}
