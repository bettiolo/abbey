using System.Collections.Generic;
using Abbey.Core;
using Abbey.EditorTools;
using Abbey.Rendering;
using Abbey.Villagers;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Abbey.Tests.EditMode
{
    public class PrototypeSpriteProjectionSceneTests
    {
        SpriteProjectionBootstrap projection;

        [OneTimeSetUp]
        public void BuildScene()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            PrototypeSceneBuilder.PopulateScene();
            projection = Object.FindFirstObjectByType<SpriteProjectionBootstrap>(
                FindObjectsInactive.Include);
        }

        [OneTimeTearDown]
        public void ClearScene()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            WorldObstacle.ClearRegistry();
        }

        [Test]
        public void SceneBuild_CreatesOneConfiguredBootstrapAndProjectsMappedTerrain()
        {
            SpriteProjectionBootstrap[] bootstraps =
                Object.FindObjectsByType<SpriteProjectionBootstrap>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.AreEqual(1, bootstraps.Length);
            Assert.AreSame(projection, bootstraps[0]);
            Assert.IsTrue(projection.ProjectionEnabled);

            var expectedTerrain = new Dictionary<string, string>
            {
                { "Ground", "map_meadow" },
                { "Beach", "map_Beach" },
                { "ForestFloor", "map_ForestFloor" },
                { "AbbeyHill", "map_abbey_hill" },
                { "Road_Beach_Camp_00", "dirt_road_segment" },
                { "StreamBank_00", "map_riverbank" },
                { "StreamWater_00", "map_stream" }
            };

            foreach (KeyValuePair<string, string> expected in expectedTerrain)
            {
                GameObject root = RequireRoot(expected.Key);
                SpriteRoleTag tag = root.GetComponent<SpriteRoleTag>();
                Assert.IsNotNull(tag, $"{expected.Key} needs a stable sprite role tag");
                Assert.AreEqual(expected.Value, tag.AssetId);
                AssertProjected(root);
            }
        }

        [Test]
        public void SceneBuild_ProjectsAllInitialVillagersWithoutChangingTaggedRoots()
        {
            VillagerAgent[] villagers = Object.FindObjectsByType<VillagerAgent>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.AreEqual(12, villagers.Length);

            for (int i = 0; i < villagers.Length; i++)
            {
                SpriteRoleTag tag = villagers[i].GetComponent<SpriteRoleTag>();
                Assert.IsNotNull(tag, $"{villagers[i].name} needs a sprite role tag");
                StringAssert.StartsWith("settler_", tag.AssetId);
                AssertProjected(villagers[i].gameObject);
            }

            SpriteRoleTag[] tags = Object.FindObjectsByType<SpriteRoleTag>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            var transforms = new Dictionary<SpriteRoleTag, TransformState>(tags.Length);
            for (int i = 0; i < tags.Length; i++)
            {
                transforms.Add(tags[i], new TransformState(tags[i].transform));
            }

            projection.ApplyAllTagged();

            foreach (KeyValuePair<SpriteRoleTag, TransformState> snapshot in transforms)
            {
                snapshot.Value.AssertUnchanged(snapshot.Key.transform, snapshot.Key.name);
            }
        }

        [Test]
        public void SceneBuild_UnresolvedSignatureActorsKeepTheir3DFallback()
        {
            AssertUnresolvedFallback("Bellkeeper", "bellkeeper_lowpoly");
            AssertUnresolvedFallback("BlackHound", "black_hound_lowpoly");
        }

        [Test]
        public void ProjectionToggle_RestoresAndRehidesLegacyVillagerVisuals()
        {
            GameObject villager = RequireRoot("Villager_00");
            var snapshot = new TransformState(villager.transform);
            SpriteRenderer sprite = SpriteProjectionFactory.GetSpriteRenderer(villager);
            Assert.IsNotNull(sprite);
            Assert.IsTrue(sprite.gameObject.activeSelf);

            try
            {
                projection.SetProjectionEnabled(false);
                Assert.IsFalse(sprite.gameObject.activeSelf);
                Assert.IsTrue(HasEnabledLegacyRenderer(villager));
                snapshot.AssertUnchanged(villager.transform, villager.name);
            }
            finally
            {
                projection.SetProjectionEnabled(true);
            }
            Assert.IsTrue(sprite.gameObject.activeSelf);
            Assert.IsFalse(HasEnabledLegacyRenderer(villager));
            snapshot.AssertUnchanged(villager.transform, villager.name);
        }

        [Test]
        public void MappedAbbeyWalls_UseAuthoredRotatedFootprints()
        {
            Rect wallA = RequireRoot("AbbeyWall_A").GetComponent<WorldObstacle>().Footprint;
            Rect wallB = RequireRoot("AbbeyWall_B").GetComponent<WorldObstacle>().Footprint;

            Assert.AreEqual(5f, wallA.width, 0.01f);
            Assert.AreEqual(1f, wallA.height, 0.01f);
            Assert.AreEqual(1f, wallB.width, 0.01f);
            Assert.AreEqual(5f, wallB.height, 0.01f);
        }

        static void AssertProjected(GameObject root)
        {
            SpriteRenderer sprite = SpriteProjectionFactory.GetSpriteRenderer(root);
            Assert.IsNotNull(sprite, $"{root.name} should resolve through the sprite catalog");
            Assert.IsTrue(sprite.gameObject.activeSelf);
            Assert.IsFalse(HasEnabledLegacyRenderer(root),
                $"{root.name} should hide its legacy 3D renderers in sprite mode");
        }

        static void AssertUnresolvedFallback(string name, string expectedAssetId)
        {
            GameObject root = RequireRoot(name);
            SpriteRoleTag tag = root.GetComponent<SpriteRoleTag>();
            Assert.IsNotNull(tag);
            Assert.AreEqual(expectedAssetId, tag.AssetId);
            Assert.IsNull(SpriteProjectionFactory.GetSpriteRenderer(root));
            Assert.IsTrue(HasEnabledLegacyRenderer(root),
                $"{name} must keep its honest 3D fallback until a signature sprite exists");
        }

        static bool HasEnabledLegacyRenderer(GameObject root)
        {
            MeshRenderer[] meshes = root.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < meshes.Length; i++)
            {
                if (meshes[i].enabled)
                {
                    return true;
                }
            }

            SkinnedMeshRenderer[] skinned = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < skinned.Length; i++)
            {
                if (skinned[i].enabled)
                {
                    return true;
                }
            }
            return false;
        }

        static GameObject RequireRoot(string name)
        {
            GameObject root = GameObject.Find(name);
            Assert.IsNotNull(root, $"Generated Map 1 root '{name}' is missing");
            return root;
        }

        readonly struct TransformState
        {
            readonly Vector3 position;
            readonly Quaternion rotation;
            readonly Vector3 scale;

            public TransformState(Transform transform)
            {
                position = transform.position;
                rotation = transform.rotation;
                scale = transform.localScale;
            }

            public void AssertUnchanged(Transform transform, string context)
            {
                Assert.AreEqual(position, transform.position, $"{context} position changed");
                Assert.AreEqual(rotation, transform.rotation, $"{context} rotation changed");
                Assert.AreEqual(scale, transform.localScale, $"{context} scale changed");
            }
        }
    }
}
