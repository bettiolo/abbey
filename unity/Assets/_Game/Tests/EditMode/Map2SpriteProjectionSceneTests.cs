using Abbey.EditorTools;
using Abbey.Rendering;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Abbey.Tests.EditMode
{
    public sealed class Map2SpriteProjectionSceneTests
    {
        EditorBuildSettingsScene[] previousBuildScenes;

        [SetUp]
        public void SetUp()
        {
            previousBuildScenes = EditorBuildSettings.scenes;
        }

        [TearDown]
        public void TearDown()
        {
            // Scene builders intentionally populate global singleton systems. Leave the
            // EditMode suite in an empty scene even when an assertion fails.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorBuildSettings.scenes = previousBuildScenes;
        }

        [Test]
        public void BuildMap2_ReusesBootstrap_ProjectsMappedArt_AndRollsBackCleanly()
        {
            Map2SceneBuilder.BuildMap2Scene();

            SpriteProjectionBootstrap[] bootstraps = Object.FindObjectsByType<SpriteProjectionBootstrap>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.That(bootstraps, Has.Length.EqualTo(1),
                "Map 2 must reuse the projection authority created while building Map 1.");
            SpriteProjectionBootstrap bootstrap = bootstraps[0];
            Assert.That(bootstrap.ProjectionEnabled, Is.True);

            AssertProjected("Map2_ForestFloor", "map_ForestFloor");
            AssertProjected("Map2_Stream", "map_stream");
            AssertProjected("DeerPath_A", "dirt_road_segment");
            AssertProjected("GroveShrine_Map2", "candle_shrine_t1");
            AssertProjected("Map2Tree_00", "forest_tree_02");

            AssertUnresolvedFallback("AbbeyOfAntlers", "abbey_cloister_t1");
            AssertUnresolvedFallback("StagBeneathAbbey", "stag_beneath_abbey_lowpoly");

            GameObject forestFloor = Require("Map2_ForestFloor");
            SpriteRenderer forestSprite = SpriteProjectionFactory.GetSpriteRenderer(forestFloor);
            bootstrap.SetProjectionEnabled(false);
            Assert.That(forestSprite.gameObject.activeSelf, Is.False);
            Assert.That(AnyLegacyRendererEnabled(forestFloor), Is.True,
                "rollback must restore the forest-floor mesh presentation");

            bootstrap.SetProjectionEnabled(true);
            Assert.That(forestSprite.gameObject.activeSelf, Is.True);
            Assert.That(AnyLegacyRendererEnabled(forestFloor), Is.False,
                "re-enabling projection must hide the restored 3D fallback again");
        }

        static void AssertProjected(string rootName, string assetId)
        {
            GameObject root = Require(rootName);
            SpriteRoleTag tag = root.GetComponent<SpriteRoleTag>();
            Assert.That(tag, Is.Not.Null, rootName);
            Assert.That(tag.AssetId, Is.EqualTo(assetId), rootName);
            Assert.That(tag.StableId, Is.EqualTo(rootName), rootName);

            SpriteRenderer sprite = SpriteProjectionFactory.GetSpriteRenderer(root);
            Assert.That(sprite, Is.Not.Null, $"{rootName} did not receive a projected sprite");
            Assert.That(sprite.sprite, Is.Not.Null, rootName);
            Assert.That(sprite.gameObject.activeSelf, Is.True, rootName);
            Assert.That(AnyLegacyRendererEnabled(root), Is.False,
                $"{rootName} retained mixed 3D art in sprite mode");
        }

        static void AssertUnresolvedFallback(string rootName, string assetId)
        {
            GameObject root = Require(rootName);
            SpriteRoleTag tag = root.GetComponent<SpriteRoleTag>();
            Assert.That(tag, Is.Not.Null, rootName);
            Assert.That(tag.AssetId, Is.EqualTo(assetId), rootName);
            Assert.That(SpriteProjectionFactory.GetSpriteRenderer(root), Is.Null,
                $"{rootName} must remain an honest 3D fallback until its identity sprite is resolved");
            Assert.That(AnyLegacyRendererEnabled(root), Is.True, rootName);
        }

        static GameObject Require(string name)
        {
            GameObject root = GameObject.Find(name);
            Assert.That(root, Is.Not.Null, $"Map 2 did not preserve landmark root '{name}'");
            return root;
        }

        static bool AnyLegacyRendererEnabled(GameObject root)
        {
            MeshRenderer[] meshes = root.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < meshes.Length; i++)
            {
                if (meshes[i].enabled) return true;
            }
            SkinnedMeshRenderer[] skinned = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < skinned.Length; i++)
            {
                if (skinned[i].enabled) return true;
            }
            return false;
        }
    }
}
