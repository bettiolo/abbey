using System.Collections.Generic;
using Abbey.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace Abbey.Tests.EditMode
{
    public class SpriteProjectionTests
    {
        readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] != null)
                {
                    Object.DestroyImmediate(_created[i]);
                }
            }
            _created.Clear();
        }

        [Test]
        public void CatalogLookup_PrefersAssetId_ThenFallsBackToRole()
        {
            SpriteProjectionCatalog catalog = Track(ScriptableObject.CreateInstance<SpriteProjectionCatalog>());
            var byAsset = new SpriteProjectionEntry { assetId = "villager_lowpoly", role = "actor" };
            var byRole = new SpriteProjectionEntry { assetId = "other", role = "actor" };
            catalog.entries.Add(byRole);
            catalog.entries.Add(byAsset);

            Assert.IsTrue(catalog.TryGet("villager_lowpoly", "actor", out SpriteProjectionEntry exact));
            Assert.AreSame(byAsset, exact);
            Assert.IsTrue(catalog.TryGet("missing", "actor", out SpriteProjectionEntry fallback));
            Assert.AreSame(byRole, fallback);
            Assert.IsFalse(catalog.TryGet("missing", "missing", out _));
        }

        [Test]
        public void EnableAndDisable_PreserveRootAndRestoreExactLegacyRendererStates()
        {
            var root = Track(new GameObject("GameplayRoot"));
            root.transform.position = new Vector3(4f, 0f, -7f);
            root.transform.rotation = Quaternion.Euler(0f, 23f, 0f);
            root.transform.localScale = new Vector3(1.5f, 2f, 0.75f);

            MeshRenderer enabledMesh = root.AddComponent<MeshRenderer>();
            var disabledChild = Track(new GameObject("DisabledLegacy"));
            disabledChild.transform.SetParent(root.transform, false);
            MeshRenderer disabledMesh = disabledChild.AddComponent<MeshRenderer>();
            disabledMesh.enabled = false;

            var cameraObject = Track(new GameObject("Camera"));
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.transform.rotation = Quaternion.Euler(30f, 45f, 0f);

            Texture2D texture = Track(new Texture2D(2, 2));
            Sprite sprite = Track(Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0f), 16f));
            var entry = new SpriteProjectionEntry
            {
                assetId = "villager_lowpoly",
                sprite = sprite,
                anchorOffset = new Vector3(0f, 0.25f, 0f),
                visualScale = 1.25f,
                sortingOffset = 3
            };

            Vector3 position = root.transform.position;
            Quaternion rotation = root.transform.rotation;
            Vector3 scale = root.transform.localScale;

            Assert.IsTrue(SpriteProjectionFactory.Enable(root, entry, camera));
            Assert.AreEqual(position, root.transform.position);
            Assert.AreEqual(rotation, root.transform.rotation);
            Assert.AreEqual(scale, root.transform.localScale);
            Assert.IsFalse(enabledMesh.enabled);
            Assert.IsFalse(disabledMesh.enabled);

            SpriteRenderer renderer = SpriteProjectionFactory.GetSpriteRenderer(root);
            Assert.IsNotNull(renderer);
            Assert.AreSame(sprite, renderer.sprite);
            Assert.Less(Quaternion.Angle(camera.transform.rotation, renderer.transform.rotation), 0.01f);
            Assert.AreEqual(position + entry.anchorOffset, renderer.transform.position);
            Assert.AreEqual(entry.visualScale, renderer.transform.lossyScale.x, 0.05f);
            Assert.AreEqual(entry.visualScale, renderer.transform.lossyScale.y, 0.05f);

            SpriteProjectionFactory.Disable(root);
            Assert.IsFalse(renderer.gameObject.activeSelf);
            Assert.IsTrue(enabledMesh.enabled);
            Assert.IsFalse(disabledMesh.enabled, "a legacy renderer that began disabled must stay disabled");
            Assert.AreEqual(position, root.transform.position);
            Assert.AreEqual(rotation, root.transform.rotation);
            Assert.AreEqual(scale, root.transform.localScale);
        }

        [Test]
        public void InvalidEntry_RestoresLegacyRendererAndHidesStaleSprite()
        {
            var root = Track(new GameObject("Root"));
            MeshRenderer mesh = root.AddComponent<MeshRenderer>();
            var cameraObject = Track(new GameObject("Camera"));
            Camera camera = cameraObject.AddComponent<Camera>();
            Texture2D texture = Track(new Texture2D(2, 2));
            Sprite sprite = Track(Sprite.Create(
                texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0f), 16f));

            Assert.IsTrue(SpriteProjectionFactory.Enable(
                root, new SpriteProjectionEntry { sprite = sprite }, camera));
            Assert.IsFalse(mesh.enabled);
            SpriteRenderer renderer = SpriteProjectionFactory.GetSpriteRenderer(root);
            Assert.IsTrue(renderer.gameObject.activeSelf);

            Assert.IsFalse(SpriteProjectionFactory.Enable(
                root, new SpriteProjectionEntry { sprite = null }, camera));
            Assert.IsTrue(mesh.enabled);
            Assert.IsFalse(renderer.gameObject.activeSelf);
        }

        [Test]
        public void Bootstrap_RegistersTaggedRoot_AndProjectionCanBeReversed()
        {
            SpriteProjectionCatalog catalog = Track(
                ScriptableObject.CreateInstance<SpriteProjectionCatalog>());
            Texture2D texture = Track(new Texture2D(2, 2));
            Sprite sprite = Track(Sprite.Create(
                texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0f), 16f));
            catalog.entries.Add(new SpriteProjectionEntry { assetId = "villager", sprite = sprite });

            var cameraObject = Track(new GameObject("Camera"));
            Camera camera = cameraObject.AddComponent<Camera>();
            var bootstrapObject = Track(new GameObject("SpriteProjection"));
            SpriteProjectionBootstrap bootstrap = bootstrapObject.AddComponent<SpriteProjectionBootstrap>();
            bootstrap.Configure(catalog, camera);

            var root = Track(new GameObject("Villager"));
            MeshRenderer mesh = root.AddComponent<MeshRenderer>();
            Assert.IsTrue(bootstrap.Register(root, "villager", stableId: "villager-01"));
            Assert.IsFalse(mesh.enabled);
            Assert.IsNotNull(SpriteProjectionFactory.GetSpriteRenderer(root));

            bootstrap.SetProjectionEnabled(false);
            Assert.IsTrue(mesh.enabled);
            Assert.IsFalse(SpriteProjectionFactory.GetSpriteRenderer(root).gameObject.activeSelf);

            bootstrap.SetProjectionEnabled(true);
            var runtimeRoot = Track(new GameObject("RuntimeVillager"));
            MeshRenderer runtimeMesh = runtimeRoot.AddComponent<MeshRenderer>();
            Assert.IsFalse(SpriteProjectionBootstrap.RegisterGlobal(
                runtimeRoot, "missing", stableId: "runtime-missing"));
            Assert.IsTrue(runtimeMesh.enabled);
            Assert.IsTrue(SpriteProjectionBootstrap.RegisterGlobal(
                runtimeRoot, "villager", stableId: "runtime-villager"));
            Assert.IsFalse(runtimeMesh.enabled);
            Assert.IsNotNull(SpriteProjectionFactory.GetSpriteRenderer(runtimeRoot));
        }

        [Test]
        public void Bootstrap_UsesPersistentConfigProjectionSwitch()
        {
            SpriteProjectionCatalog catalog = Track(
                ScriptableObject.CreateInstance<SpriteProjectionCatalog>());
            Texture2D texture = Track(new Texture2D(2, 2));
            Sprite sprite = Track(Sprite.Create(
                texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0f), 16f));
            catalog.entries.Add(new SpriteProjectionEntry { assetId = "actor", sprite = sprite });
            SpriteProjectionStyle style = Track(
                ScriptableObject.CreateInstance<SpriteProjectionStyle>());
            SpriteProjectionConfig config = Track(
                ScriptableObject.CreateInstance<SpriteProjectionConfig>());
            config.projectionEnabled = false;
            config.style = style;
            var bootstrapObject = Track(new GameObject("Projection"));
            SpriteProjectionBootstrap bootstrap = bootstrapObject.AddComponent<SpriteProjectionBootstrap>();
            bootstrap.Configure(catalog, null, style, config);
            var root = Track(new GameObject("LegacyActor"));
            MeshRenderer mesh = root.AddComponent<MeshRenderer>();

            Assert.IsFalse(bootstrap.Register(root, "actor", stableId: "legacy-actor"));
            Assert.IsFalse(bootstrap.ProjectionEnabled);
            Assert.IsTrue(mesh.enabled);
            Assert.IsNull(SpriteProjectionFactory.GetSpriteRenderer(root));
        }

        [Test]
        public void ProjectedDepth_SortsNearRootInFrontOfFarRoot()
        {
            var cameraObject = Track(new GameObject("Camera"));
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 10f, -10f);
            camera.transform.rotation = Quaternion.Euler(30f, 0f, 0f);
            Texture2D texture = Track(new Texture2D(2, 2));
            Sprite sprite = Track(Sprite.Create(
                texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0f), 16f));
            var entry = new SpriteProjectionEntry { sprite = sprite };

            var near = Track(new GameObject("Near"));
            near.transform.position = Vector3.zero;
            var far = Track(new GameObject("Far"));
            far.transform.position = new Vector3(0f, 0f, 8f);
            SpriteProjectionFactory.Enable(near, entry, camera);
            SpriteProjectionFactory.Enable(far, entry, camera);

            Assert.Greater(
                SpriteProjectionFactory.GetSpriteRenderer(near).sortingOrder,
                SpriteProjectionFactory.GetSpriteRenderer(far).sortingOrder);
        }

        [Test]
        public void EqualDepth_UsesUniqueStableRanksAcrossRepeatedApplication()
        {
            var cameraObject = Track(new GameObject("Camera"));
            Camera camera = cameraObject.AddComponent<Camera>();
            Texture2D texture = Track(new Texture2D(2, 2));
            Sprite sprite = Track(Sprite.Create(
                texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0f), 16f));
            SpriteProjectionCatalog catalog = Track(
                ScriptableObject.CreateInstance<SpriteProjectionCatalog>());
            catalog.entries.Add(new SpriteProjectionEntry { assetId = "actor", sprite = sprite });
            var bootstrapObject = Track(new GameObject("Projection"));
            SpriteProjectionBootstrap bootstrap = bootstrapObject.AddComponent<SpriteProjectionBootstrap>();
            bootstrap.Configure(catalog, camera);

            var rootB = Track(new GameObject("B"));
            rootB.transform.position = Vector3.zero;
            bootstrap.Register(rootB, "actor", stableId: "b");
            var rootA = Track(new GameObject("A"));
            rootA.transform.position = Vector3.zero;
            bootstrap.Register(rootA, "actor", stableId: "a");

            int firstA = SpriteProjectionFactory.GetSpriteRenderer(rootA).sortingOrder;
            int firstB = SpriteProjectionFactory.GetSpriteRenderer(rootB).sortingOrder;
            Assert.AreNotEqual(firstA, firstB);
            bootstrap.ApplyAllTagged();
            Assert.AreEqual(firstA, SpriteProjectionFactory.GetSpriteRenderer(rootA).sortingOrder);
            Assert.AreEqual(firstB, SpriteProjectionFactory.GetSpriteRenderer(rootB).sortingOrder);
            Assert.Less(rootA.GetComponent<SpriteRoleTag>().DeterministicSortIndex,
                rootB.GetComponent<SpriteRoleTag>().DeterministicSortIndex);
        }

        [Test]
        public void GroundLayout_TilesAcrossLocalXZFootprintWithoutChangingRootTransform()
        {
            GameObject root = Track(GameObject.CreatePrimitive(PrimitiveType.Plane));
            root.transform.position = new Vector3(3f, 0.1f, -4f);
            root.transform.rotation = Quaternion.Euler(0f, 27f, 0f);
            root.transform.localScale = new Vector3(8f, 1f, 6f);
            Texture2D texture = Track(new Texture2D(16, 16));
            Sprite sprite = Track(Sprite.Create(
                texture, new Rect(0f, 0f, 16f, 16f), new Vector2(0.5f, 0.5f), 16f));
            var entry = new SpriteProjectionEntry
            {
                sprite = sprite,
                layout = SpriteProjectionLayout.GroundTiled
            };
            Vector3 position = root.transform.position;
            Quaternion rotation = root.transform.rotation;
            Vector3 scale = root.transform.localScale;

            Assert.IsTrue(SpriteProjectionFactory.Enable(root, entry, null));

            SpriteRenderer renderer = SpriteProjectionFactory.GetSpriteRenderer(root);
            Assert.AreEqual(SpriteDrawMode.Tiled, renderer.drawMode);
            Assert.AreEqual(10f, renderer.size.x, 0.01f);
            Assert.AreEqual(10f, renderer.size.y, 0.01f);
            Assert.Less(Quaternion.Angle(
                root.transform.rotation * Quaternion.Euler(90f, 0f, 0f),
                renderer.transform.rotation), 0.01f);
            Assert.AreEqual(position, root.transform.position);
            Assert.AreEqual(rotation, root.transform.rotation);
            Assert.AreEqual(scale, root.transform.localScale);
        }

        T Track<T>(T value) where T : Object
        {
            _created.Add(value);
            return value;
        }
    }
}
