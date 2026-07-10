using System;
using System.Collections.Generic;
using System.IO;
using Abbey.Editor;
using Abbey.Rendering;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Abbey.Tests.EditMode
{
    public class MiniWorldSpriteImportTests
    {
        [OneTimeSetUp]
        public void ImportFixture()
        {
            MiniWorldSpriteImporter.ImportAllAndRebuildCatalog();
        }

        [Test]
        public void Manifest_IsDeterministicAndInternallyValid()
        {
            MiniWorldManifest manifest = MiniWorldSpriteImporter.LoadManifest();
            List<string> errors = MiniWorldProjectionValidator.CollectManifestErrors(manifest);

            Assert.That(errors, Is.Empty, string.Join("\n", errors));
            Assert.That(manifest.files, Has.Length.EqualTo(32));
            Assert.That(manifest.entries, Has.Length.EqualTo(61));
        }

        [Test]
        public void SelectedTextures_MatchCompleteImportContractAndExactSlices()
        {
            MiniWorldManifest manifest = MiniWorldSpriteImporter.LoadManifest();
            for (int fileIndex = 0; fileIndex < manifest.files.Length; fileIndex++)
            {
                MiniWorldFile file = manifest.files[fileIndex];
                string path = MiniWorldSpriteImporter.ToAssetPath(file.abbeyPath);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.That(importer, Is.Not.Null, file.fileId);
                Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite), file.fileId);
                Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Multiple), file.fileId);
                Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(16f), file.fileId);
                Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point), file.fileId);
                Assert.That(importer.mipmapEnabled, Is.False, file.fileId);
                Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed), file.fileId);
                Assert.That(importer.npotScale, Is.EqualTo(TextureImporterNPOTScale.None), file.fileId);
                Assert.That(importer.sRGBTexture, Is.True, file.fileId);
                Assert.That(importer.alphaIsTransparency, Is.True, file.fileId);
                Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp), file.fileId);
                Assert.That(importer.isReadable, Is.False, file.fileId);

                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                Assert.That(settings.spriteMeshType, Is.EqualTo(SpriteMeshType.FullRect), file.fileId);
                Assert.That(settings.spriteGenerateFallbackPhysicsShape, Is.False, file.fileId);

                SpriteMetaData[] actual = importer.spritesheet;
                Assert.That(actual, Has.Length.EqualTo(file.slices.Length), file.fileId);
                for (int sliceIndex = 0; sliceIndex < file.slices.Length; sliceIndex++)
                {
                    MiniWorldSlice expected = file.slices[sliceIndex];
                    Assert.That(actual[sliceIndex].name, Is.EqualTo(expected.name));
                    Assert.That(actual[sliceIndex].rect, Is.EqualTo(expected.rect.Rect));
                    Assert.That(actual[sliceIndex].alignment, Is.EqualTo((int)SpriteAlignment.Custom));
                    Assert.That(actual[sliceIndex].pivot, Is.EqualTo(file.Pivot));
                    Assert.That(actual[sliceIndex].border, Is.EqualTo(Vector4.zero));
                }
            }
        }

        [Test]
        public void Catalog_MapsEveryManifestEntryExactlyOnceWithoutNullsOrDuplicates()
        {
            MiniWorldManifest manifest = MiniWorldSpriteImporter.LoadManifest();
            SpriteProjectionCatalog catalog = AssetDatabase.LoadAssetAtPath<SpriteProjectionCatalog>(
                MiniWorldSpriteImporter.CatalogAssetPath);

            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.entries, Has.Count.EqualTo(manifest.entries.Length));
            var assetIds = new HashSet<string>(StringComparer.Ordinal);
            var assetRolePairs = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < catalog.entries.Count; i++)
            {
                SpriteProjectionEntry actual = catalog.entries[i];
                MiniWorldEntry expected = manifest.entries[i];
                Assert.That(actual, Is.Not.Null);
                Assert.That(actual.sprite, Is.Not.Null, expected.assetId);
                Assert.That(actual.assetId, Is.EqualTo(expected.assetId));
                Assert.That(actual.role, Is.EqualTo(expected.roles[0]));
                Assert.That(actual.visualScale, Is.EqualTo(expected.visualScale));
                Assert.That(actual.anchorOffset, Is.EqualTo(expected.AnchorOffset));
                Assert.That(actual.sortingOffset, Is.EqualTo(expected.roleSortOffset));
                Assert.That(actual.authoredFootprint, Is.EqualTo(expected.AuthoredFootprint));
                if (expected.HasWalkAnimationData)
                {
                    Assert.That(actual.HasDirectionalSprites, Is.True, expected.assetId);
                    Assert.That(actual.southWalk, Has.Length.EqualTo(
                        expected.walkAnimation.directions.south.Length), expected.assetId);
                    Assert.That(actual.northWalk, Has.Length.EqualTo(
                        expected.walkAnimation.directions.north.Length), expected.assetId);
                    Assert.That(actual.eastWalk, Has.Length.EqualTo(
                        expected.walkAnimation.directions.east.Length), expected.assetId);
                    Assert.That(actual.westWalk, Has.Length.EqualTo(
                        expected.walkAnimation.directions.west.Length), expected.assetId);
                }
                Assert.That(assetIds.Add(actual.assetId), Is.True, $"duplicate asset id {actual.assetId}");
                Assert.That(assetRolePairs.Add(actual.assetId + "\n" + actual.role), Is.True,
                    $"duplicate asset/role pair {actual.assetId} / {actual.role}");
            }
        }

        [Test]
        public void WallAndTowerMappings_HavePositiveAuthoredFootprints()
        {
            MiniWorldManifest manifest = MiniWorldSpriteImporter.LoadManifest();
            int asserted = 0;
            for (int i = 0; i < manifest.entries.Length; i++)
            {
                MiniWorldEntry entry = manifest.entries[i];
                string role = entry.roles[0];
                if (!ContainsWallOrTower(entry.assetId) && !ContainsWallOrTower(role)) continue;

                asserted++;
                Assert.That(entry.authoredFootprint, Is.Not.Null, entry.assetId);
                Assert.That(entry.authoredFootprint, Has.Length.EqualTo(2), entry.assetId);
                Assert.That(entry.authoredFootprint[0], Is.GreaterThan(0f), entry.assetId);
                Assert.That(entry.authoredFootprint[1], Is.GreaterThan(0f), entry.assetId);
            }
            Assert.That(asserted, Is.GreaterThan(0), "fixture must exercise wall/tower footprints");
        }

        [Test]
        public void Validation_DetectsDuplicateKeysAndCatalogDrift()
        {
            MiniWorldManifest manifest = MiniWorldSpriteImporter.LoadManifest();
            MiniWorldEntry original = manifest.entries[1];
            string originalAssetId = original.assetId;
            string originalRole = original.roles[0];
            try
            {
                original.assetId = manifest.entries[0].assetId;
                original.roles[0] = manifest.entries[0].roles[0];
                List<string> errors = MiniWorldProjectionValidator.CollectManifestErrors(manifest);
                Assert.That(errors.Exists(error => error.Contains("Duplicate or empty assetId")), Is.True);
                Assert.That(errors.Exists(error => error.Contains("Duplicate asset/role pair")), Is.True);
            }
            finally
            {
                original.assetId = originalAssetId;
                original.roles[0] = originalRole;
            }

            SpriteProjectionCatalog catalog = AssetDatabase.LoadAssetAtPath<SpriteProjectionCatalog>(
                MiniWorldSpriteImporter.CatalogAssetPath);
            float originalScale = catalog.entries[0].visualScale;
            try
            {
                catalog.entries[0].visualScale = originalScale + 1f;
                List<string> errors = MiniWorldProjectionValidator.CollectProjectErrors(manifest);
                Assert.That(errors.Exists(error => error.Contains("Catalog mapping drift")), Is.True);
            }
            finally
            {
                catalog.entries[0].visualScale = originalScale;
            }
        }

        [Test]
        public void Validation_RejectsCanonicalPathTraversalBeforeFileAccess()
        {
            MiniWorldManifest manifest = MiniWorldSpriteImporter.LoadManifest();
            string original = manifest.files[0].abbeyPath;
            try
            {
                manifest.files[0].abbeyPath =
                    "unity/Assets/_Game/Art/Placeholders/MerchantShadeMiniWorld/../escape.png";
                Assert.That(MiniWorldSpriteImporter.TryResolveCuratedAssetPath(
                    manifest.files[0].abbeyPath, out _, out _), Is.False);
                List<string> errors = MiniWorldProjectionValidator.CollectManifestErrors(manifest);
                Assert.That(errors.Exists(error => error.Contains("outside the curated PNG root")),
                    Is.True);
            }
            finally
            {
                manifest.files[0].abbeyPath = original;
            }
        }

        [Test]
        public void ReimportingTwice_IsByteStable()
        {
            MiniWorldSpriteImporter.ImportAllAndRebuildCatalog();
            Dictionary<string, string> first = SnapshotGeneratedFiles();

            MiniWorldSpriteImporter.ImportAllAndRebuildCatalog();
            Dictionary<string, string> second = SnapshotGeneratedFiles();

            Assert.That(second.Keys, Is.EquivalentTo(first.Keys));
            foreach (KeyValuePair<string, string> pair in first)
            {
                Assert.That(second[pair.Key], Is.EqualTo(pair.Value), pair.Key);
            }
        }

        static Dictionary<string, string> SnapshotGeneratedFiles()
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            MiniWorldManifest manifest = MiniWorldSpriteImporter.LoadManifest();
            for (int i = 0; i < manifest.files.Length; i++)
            {
                string metaPath = Path.GetFullPath(
                    MiniWorldSpriteImporter.ToAssetPath(manifest.files[i].abbeyPath) + ".meta");
                result.Add(metaPath, File.ReadAllText(metaPath));
            }
            string catalogPath = Path.GetFullPath(MiniWorldSpriteImporter.CatalogAssetPath);
            result.Add(catalogPath, File.ReadAllText(catalogPath));
            result.Add(catalogPath + ".meta", File.ReadAllText(catalogPath + ".meta"));
            return result;
        }

        static bool ContainsWallOrTower(string value)
        {
            return value.IndexOf("wall", StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("tower", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
