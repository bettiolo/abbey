using System.Collections.Generic;
using Abbey.EditorTools;
using NUnit.Framework;
using UnityEngine;

namespace Abbey.Tests.EditMode
{
    /// <summary>
    /// Exercises the import-validation logic on fake hierarchies and raw metadata
    /// JSON — no glTFast import, no AssetDatabase. The importer menu items reuse
    /// exactly these methods, so a green run here proves the validator itself.
    /// </summary>
    public class GeneratedAssetValidatorTests
    {
        readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }
            _spawned.Clear();
        }

        GameObject Spawn(string name, Transform parent = null)
        {
            var go = new GameObject(name);
            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }
            else
            {
                _spawned.Add(go);
            }
            return go;
        }

        // ------------------------------------------------------------------
        // Metadata parsing
        // ------------------------------------------------------------------

        const string CampfireMetaSnippet = @"{
            ""id"": ""campfire_t1"",
            ""spec"": {
                ""id"": ""campfire_t1"",
                ""anchors"": [ { ""name"": ""should_be_ignored"", ""type"": ""light"" } ]
            },
            ""anchors"": [
                { ""name"": ""smoke"", ""type"": ""particle"" },
                { ""name"": ""ember_glow"", ""type"": ""light"" }
            ]
        }";

        [Test]
        public void ParseAnchorNames_ReadsTopLevelAnchors_NotTheSpecCopy()
        {
            var names = GeneratedAssetValidator.ParseAnchorNames(CampfireMetaSnippet);

            Assert.AreEqual(2, names.Length);
            CollectionAssert.AreEquivalent(new[] { "smoke", "ember_glow" }, names);
            CollectionAssert.DoesNotContain(names, "should_be_ignored");
        }

        [Test]
        public void ParseAnchorNames_EmptyOrAnchorlessMetadata_YieldsEmpty()
        {
            Assert.AreEqual(0, GeneratedAssetValidator.ParseAnchorNames(null).Length);
            Assert.AreEqual(0, GeneratedAssetValidator.ParseAnchorNames("").Length);
            Assert.AreEqual(0, GeneratedAssetValidator.ParseAnchorNames(@"{ ""id"": ""x"" }").Length);
        }

        // ------------------------------------------------------------------
        // Hierarchy validation
        // ------------------------------------------------------------------

        [Test]
        public void FindMissingAnchors_AllPresent_EvenNested_ReturnsEmpty()
        {
            var root = Spawn("campfire_t1");
            var body = Spawn("body", root.transform);
            Spawn("smoke", root.transform);
            Spawn("ember_glow", body.transform); // nested one level down

            var missing = GeneratedAssetValidator.FindMissingAnchors(
                root.transform, new[] { "smoke", "ember_glow" });

            Assert.IsEmpty(missing);
        }

        [Test]
        public void FindMissingAnchors_ReportsExactlyTheAbsentOnes()
        {
            var root = Spawn("bell_tower_ruined");
            Spawn("bell", root.transform);
            Spawn("door", root.transform);

            var missing = GeneratedAssetValidator.FindMissingAnchors(
                root.transform, new[] { "bell", "hound_lair", "door" });

            Assert.AreEqual(1, missing.Count);
            Assert.AreEqual("hound_lair", missing[0]);
        }

        [Test]
        public void FindMissingAnchors_NameMatchIsExact()
        {
            var root = Spawn("asset");
            Spawn("smoke_extra", root.transform); // superstring must NOT satisfy "smoke"

            var missing = GeneratedAssetValidator.FindMissingAnchors(
                root.transform, new[] { "smoke" });

            Assert.AreEqual(1, missing.Count);
        }

        [Test]
        public void FindMissingAnchors_NoAnchorsDeclared_PassesTrivially()
        {
            var root = Spawn("plain_asset");

            Assert.IsEmpty(GeneratedAssetValidator.FindMissingAnchors(
                root.transform, new string[0]));
            Assert.IsEmpty(GeneratedAssetValidator.FindMissingAnchors(root.transform, null));
        }

        [Test]
        public void FindMissingAnchors_NullRoot_ReportsEverythingMissing()
        {
            var missing = GeneratedAssetValidator.FindMissingAnchors(
                null, new[] { "smoke", "door" });

            Assert.AreEqual(2, missing.Count);
        }

        // ------------------------------------------------------------------
        // End-to-end against the real committed metadata + a fake import
        // ------------------------------------------------------------------

        [Test]
        public void CampfireMetadataShape_ValidatesAgainstAFakeImportHierarchy()
        {
            // A stand-in for what glTFast produces: root with mesh + anchor children.
            var root = Spawn("campfire_t1");
            Spawn("campfire_t1_mesh", root.transform);
            Spawn("smoke", root.transform);
            Spawn("ember_glow", root.transform);

            var names = GeneratedAssetValidator.ParseAnchorNames(CampfireMetaSnippet);
            var missing = GeneratedAssetValidator.FindMissingAnchors(root.transform, names);

            Assert.IsEmpty(missing, "the fake import satisfies its metadata anchors");
        }
    }
}
