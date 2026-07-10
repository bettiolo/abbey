using System;
using System.Collections.Generic;
using System.IO;
using Abbey.Rendering;
using UnityEditor;
using UnityEngine;

namespace Abbey.Editor
{
    /// <summary>
    /// Deterministically applies the committed Mini World manifest to Unity texture imports
    /// and regenerates the runtime projection catalog. The manifest is the source of truth.
    /// </summary>
    public sealed class MiniWorldSpriteImporter : AssetPostprocessor
    {
        public const string ManifestAssetPath =
            "Assets/_Game/Art/Placeholders/MerchantShadeMiniWorld/manifest.json";
        public const string CuratedAssetRoot =
            "Assets/_Game/Art/Placeholders/MerchantShadeMiniWorld/";
        public const string CatalogAssetPath =
            "Assets/_Game/Settings/Rendering/MiniWorldSpriteProjectionCatalog.asset";

        static MiniWorldManifest cachedManifest;

        void OnPreprocessTexture()
        {
            if (!IsCuratedPng(assetPath))
            {
                return;
            }

            MiniWorldManifest manifest = LoadManifest();
            MiniWorldFile file = FindFileByAssetPath(manifest, assetPath);
            if (file == null)
            {
                throw new InvalidOperationException(
                    $"Mini World PNG is not declared in the manifest: {assetPath}");
            }

            ApplyImportSettings((TextureImporter)assetImporter, file);
        }

        [MenuItem("Tools/Abbey/Sprites/Import Mini World Sprites")]
        public static void ImportAllAndRebuildCatalog()
        {
            cachedManifest = null;
            MiniWorldManifest manifest = LoadManifest();
            MiniWorldProjectionValidator.ThrowIfManifestInvalid(manifest);

            for (int i = 0; i < manifest.files.Length; i++)
            {
                MiniWorldFile file = manifest.files[i];
                string assetPath = ToAssetPath(file.abbeyPath);
                TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null)
                {
                    throw new InvalidOperationException($"No TextureImporter found for {assetPath}");
                }

                ApplyImportSettings(importer, file);
                importer.SaveAndReimport();
            }

            RebuildCatalog(manifest);
            AssetDatabase.SaveAssets();
            MiniWorldProjectionValidator.ThrowIfProjectInvalid(manifest);
            Debug.Log(
                $"Imported {manifest.files.Length} Mini World sprite sheets and generated " +
                $"{manifest.entries.Length} catalog entries.");
        }

        public static MiniWorldManifest LoadManifest()
        {
            if (cachedManifest != null)
            {
                return cachedManifest;
            }

            string fullPath = Path.GetFullPath(ManifestAssetPath);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException("Mini World manifest is missing.", fullPath);
            }

            cachedManifest = JsonUtility.FromJson<MiniWorldManifest>(File.ReadAllText(fullPath));
            if (cachedManifest == null)
            {
                throw new InvalidOperationException("Mini World manifest could not be parsed.");
            }
            return cachedManifest;
        }

        public static void ApplyImportSettings(TextureImporter importer, MiniWorldFile file)
        {
            if (importer == null)
            {
                throw new ArgumentNullException(nameof(importer));
            }
            if (file == null)
            {
                throw new ArgumentNullException(nameof(file));
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = string.Equals(file.importMode, "multiple", StringComparison.Ordinal)
                ? SpriteImportMode.Multiple
                : SpriteImportMode.Single;
            importer.spritePixelsPerUnit = file.pixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.sRGBTexture = true;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.isReadable = false;
            var textureSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(textureSettings);
            textureSettings.spriteMeshType = SpriteMeshType.FullRect;
            textureSettings.spriteAlignment = (int)SpriteAlignment.Custom;
            textureSettings.spritePivot = file.Pivot;
            textureSettings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(textureSettings);

            var sprites = new SpriteMetaData[file.slices.Length];
            for (int i = 0; i < file.slices.Length; i++)
            {
                MiniWorldSlice slice = file.slices[i];
                sprites[i] = new SpriteMetaData
                {
                    name = slice.name,
                    rect = slice.rect.Rect,
                    alignment = (int)SpriteAlignment.Custom,
                    pivot = file.Pivot,
                    border = Vector4.zero
                };
            }
            importer.spritesheet = sprites;
        }

        public static void RebuildCatalog(MiniWorldManifest manifest)
        {
            if (manifest == null)
            {
                throw new ArgumentNullException(nameof(manifest));
            }

            SpriteProjectionCatalog catalog =
                AssetDatabase.LoadAssetAtPath<SpriteProjectionCatalog>(CatalogAssetPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<SpriteProjectionCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogAssetPath);
            }

            var filesById = new Dictionary<string, MiniWorldFile>(StringComparer.Ordinal);
            for (int i = 0; i < manifest.files.Length; i++)
            {
                filesById.Add(manifest.files[i].fileId, manifest.files[i]);
            }

            var spriteCache = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            catalog.entries.Clear();
            for (int i = 0; i < manifest.entries.Length; i++)
            {
                MiniWorldEntry source = manifest.entries[i];
                Sprite sprite = ResolveSprite(source.defaultSprite, filesById, spriteCache);
                string fileId = source.defaultSprite.Split(':')[0];
                MiniWorldFile sourceFile = filesById[fileId];
                catalog.entries.Add(new SpriteProjectionEntry
                {
                    assetId = source.assetId,
                    role = source.roles != null && source.roles.Length > 0 ? source.roles[0] : string.Empty,
                    sprite = sprite,
                    southSprite = ResolveOptionalSprite(
                        source.HasDirectionalSpriteData ? source.directionalSprites.south : null,
                        filesById, spriteCache),
                    northSprite = ResolveOptionalSprite(
                        source.HasDirectionalSpriteData ? source.directionalSprites.north : null,
                        filesById, spriteCache),
                    eastSprite = ResolveOptionalSprite(
                        source.HasDirectionalSpriteData ? source.directionalSprites.east : null,
                        filesById, spriteCache),
                    westSprite = ResolveOptionalSprite(
                        source.HasDirectionalSpriteData ? source.directionalSprites.west : null,
                        filesById, spriteCache),
                    southWalk = ResolveFrames(source.HasWalkAnimationData
                        ? source.walkAnimation.directions.south : null, filesById, spriteCache),
                    northWalk = ResolveFrames(source.HasWalkAnimationData
                        ? source.walkAnimation.directions.north : null, filesById, spriteCache),
                    eastWalk = ResolveFrames(source.HasWalkAnimationData
                        ? source.walkAnimation.directions.east : null, filesById, spriteCache),
                    westWalk = ResolveFrames(source.HasWalkAnimationData
                        ? source.walkAnimation.directions.west : null, filesById, spriteCache),
                    walkFrameSeconds = source.HasWalkAnimationData
                        ? source.walkAnimation.frameSeconds
                        : 0.2f,
                    layout = string.Equals(sourceFile.orientation, "xzTile", StringComparison.Ordinal)
                        ? SpriteProjectionLayout.GroundTiled
                        : SpriteProjectionLayout.CameraFacing,
                    visualScale = source.visualScale,
                    anchorOffset = source.AnchorOffset,
                    sortingOffset = source.roleSortOffset,
                    participatesInPhaseTint = source.phaseTint,
                    authoredFootprint = source.AuthoredFootprint
                });
            }

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssetIfDirty(catalog);
        }

        static Sprite ResolveOptionalSprite(
            string spriteReference,
            Dictionary<string, MiniWorldFile> filesById,
            Dictionary<string, Sprite> spriteCache)
        {
            return string.IsNullOrEmpty(spriteReference)
                ? null
                : ResolveSprite(spriteReference, filesById, spriteCache);
        }

        static Sprite[] ResolveFrames(
            string[] references,
            Dictionary<string, MiniWorldFile> filesById,
            Dictionary<string, Sprite> spriteCache)
        {
            if (references == null || references.Length == 0)
            {
                return Array.Empty<Sprite>();
            }
            var frames = new Sprite[references.Length];
            for (int i = 0; i < references.Length; i++)
            {
                frames[i] = ResolveSprite(references[i], filesById, spriteCache);
            }
            return frames;
        }

        public static string ToAssetPath(string repositoryPath)
        {
            const string unityPrefix = "unity/";
            if (string.IsNullOrEmpty(repositoryPath))
            {
                return string.Empty;
            }
            return repositoryPath.StartsWith(unityPrefix, StringComparison.Ordinal)
                ? repositoryPath.Substring(unityPrefix.Length)
                : repositoryPath;
        }

        public static bool TryResolveCuratedAssetPath(
            string repositoryPath,
            out string assetPath,
            out string absolutePath)
        {
            assetPath = ToAssetPath(repositoryPath);
            absolutePath = string.Empty;
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return false;
            }

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string curatedRoot = Path.GetFullPath(Path.Combine(projectRoot, CuratedAssetRoot));
            string candidate = Path.GetFullPath(Path.Combine(projectRoot, assetPath));
            string rootWithSeparator = curatedRoot.TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            if (!candidate.StartsWith(rootWithSeparator, StringComparison.Ordinal) ||
                !candidate.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            absolutePath = candidate;
            assetPath = "Assets/" + Path.GetRelativePath(Application.dataPath, candidate)
                .Replace(Path.DirectorySeparatorChar, '/');
            return true;
        }

        static Sprite ResolveSprite(
            string spriteReference,
            Dictionary<string, MiniWorldFile> filesById,
            Dictionary<string, Sprite> spriteCache)
        {
            if (spriteCache.TryGetValue(spriteReference, out Sprite cached))
            {
                return cached;
            }

            int separator = string.IsNullOrEmpty(spriteReference) ? -1 : spriteReference.IndexOf(':');
            if (separator <= 0 || separator >= spriteReference.Length - 1)
            {
                throw new InvalidOperationException($"Invalid sprite reference '{spriteReference}'.");
            }

            string fileId = spriteReference.Substring(0, separator);
            string spriteName = spriteReference.Substring(separator + 1);
            if (!filesById.TryGetValue(fileId, out MiniWorldFile file))
            {
                throw new InvalidOperationException(
                    $"Sprite reference '{spriteReference}' names unknown file '{fileId}'.");
            }

            string assetPath = ToAssetPath(file.abbeyPath);
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            Sprite match = null;
            for (int i = 0; i < assets.Length; i++)
            {
                if (!(assets[i] is Sprite candidate)
                    || !string.Equals(candidate.name, spriteName, StringComparison.Ordinal))
                {
                    continue;
                }
                if (match != null)
                {
                    throw new InvalidOperationException(
                        $"Sprite '{spriteName}' resolves more than once in {assetPath}.");
                }
                match = candidate;
            }

            if (match == null)
            {
                throw new InvalidOperationException(
                    $"Sprite '{spriteName}' was not imported from {assetPath}.");
            }

            spriteCache.Add(spriteReference, match);
            return match;
        }

        static MiniWorldFile FindFileByAssetPath(MiniWorldManifest manifest, string candidatePath)
        {
            if (manifest.files == null)
            {
                return null;
            }
            for (int i = 0; i < manifest.files.Length; i++)
            {
                MiniWorldFile file = manifest.files[i];
                if (string.Equals(ToAssetPath(file.abbeyPath), candidatePath, StringComparison.Ordinal))
                {
                    return file;
                }
            }
            return null;
        }

        static bool IsCuratedPng(string candidatePath)
        {
            return candidatePath.StartsWith(CuratedAssetRoot, StringComparison.Ordinal)
                && candidatePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                && !candidatePath.EndsWith("contact-sheet.png", StringComparison.OrdinalIgnoreCase);
        }
    }

    [Serializable]
    public sealed class MiniWorldManifest
    {
        public int schemaVersion;
        public string rectOrigin;
        public MiniWorldFile[] files;
        public MiniWorldEntry[] entries;
    }

    [Serializable]
    public sealed class MiniWorldFile
    {
        public string fileId;
        public string category;
        public string abbeyPath;
        public MiniWorldDimensions dimensions;
        public MiniWorldDimensions expectedDimensions;
        public MiniWorldDimensions sheetCellSize;
        public string importMode;
        public int pixelsPerUnit;
        public string orientation;
        public float[] pivot;
        public MiniWorldSlice[] slices;

        public Vector2 Pivot => pivot != null && pivot.Length == 2
            ? new Vector2(pivot[0], pivot[1])
            : Vector2.zero;
    }

    [Serializable]
    public sealed class MiniWorldDimensions
    {
        public int width;
        public int height;
    }

    [Serializable]
    public sealed class MiniWorldSlice
    {
        public string name;
        public MiniWorldRect rect;
    }

    [Serializable]
    public sealed class MiniWorldRect
    {
        public int x;
        public int y;
        public int width;
        public int height;

        public Rect Rect => new Rect(x, y, width, height);
    }

    [Serializable]
    public sealed class MiniWorldEntry
    {
        public string assetId;
        public string[] roles;
        public string defaultSprite;
        public MiniWorldDirectionalSprites directionalSprites;
        public MiniWorldWalkAnimation walkAnimation;
        public float visualScale;
        public float[] anchorOffset;
        public int roleSortOffset;
        public bool phaseTint;
        public float[] authoredFootprint;

        public Vector3 AnchorOffset => anchorOffset != null && anchorOffset.Length == 2
            ? new Vector3(anchorOffset[0], anchorOffset[1], 0f)
            : Vector3.zero;

        public Vector2 AuthoredFootprint => authoredFootprint != null && authoredFootprint.Length == 2
            ? new Vector2(authoredFootprint[0], authoredFootprint[1])
            : Vector2.zero;

        public bool HasDirectionalSpriteData => directionalSprites != null &&
            (!string.IsNullOrEmpty(directionalSprites.south) ||
             !string.IsNullOrEmpty(directionalSprites.north) ||
             !string.IsNullOrEmpty(directionalSprites.east) ||
             !string.IsNullOrEmpty(directionalSprites.west));

        public bool HasWalkAnimationData => walkAnimation != null &&
            walkAnimation.directions != null &&
            (walkAnimation.frameSeconds > 0f ||
             HasFrames(walkAnimation.directions.south) ||
             HasFrames(walkAnimation.directions.north) ||
             HasFrames(walkAnimation.directions.east) ||
             HasFrames(walkAnimation.directions.west));

        static bool HasFrames(string[] frames) => frames != null && frames.Length > 0;
    }

    [Serializable]
    public sealed class MiniWorldDirectionalSprites
    {
        public string south;
        public string north;
        public string east;
        public string west;
    }

    [Serializable]
    public sealed class MiniWorldWalkAnimation
    {
        public float frameSeconds;
        public MiniWorldDirectionalFrames directions;
    }

    [Serializable]
    public sealed class MiniWorldDirectionalFrames
    {
        public string[] south;
        public string[] north;
        public string[] east;
        public string[] west;
    }
}
