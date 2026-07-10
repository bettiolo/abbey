using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abbey.Rendering
{
    /// <summary>Single scene authority for applying and reversing tagged sprite visuals.</summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class SpriteProjectionBootstrap : MonoBehaviour
    {
        [SerializeField] SpriteProjectionCatalog catalog;
        [SerializeField] SpriteProjectionConfig config;
        [SerializeField] SpriteProjectionStyle style;
        [SerializeField] Camera targetCamera;
        [SerializeField] bool projectionEnabled = true;
        readonly List<SpriteProjectionVisual> sortedVisuals = new List<SpriteProjectionVisual>();

        public static SpriteProjectionBootstrap Instance { get; private set; }
        public SpriteProjectionCatalog Catalog => catalog;
        public SpriteProjectionConfig Config => config;
        public bool ProjectionEnabled => projectionEnabled;

        void Awake()
        {
            Instance = this;
            EnsureDependencies();
        }

        void OnEnable()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        void LateUpdate()
        {
            RefreshSortOrder();
        }

        public void Configure(
            SpriteProjectionCatalog newCatalog,
            Camera camera,
            SpriteProjectionStyle newStyle = null,
            SpriteProjectionConfig newConfig = null)
        {
            Instance = this;
            catalog = newCatalog;
            targetCamera = camera;
            config = newConfig;
            style = newStyle != null ? newStyle : config != null ? config.style : null;
            if (config != null)
            {
                projectionEnabled = config.projectionEnabled;
            }
            EnsureDependencies();
        }

        public bool Register(
            GameObject root,
            string assetId,
            string role = null,
            string stableId = null)
        {
            if (root == null)
            {
                return false;
            }
            SpriteRoleTag tag = root.GetComponent<SpriteRoleTag>();
            if (tag == null)
            {
                tag = root.AddComponent<SpriteRoleTag>();
            }
            tag.Configure(assetId, role, string.IsNullOrEmpty(stableId) ? root.name : stableId);
            ApplyAllTagged();
            return SpriteProjectionFactory.GetSpriteRenderer(root) != null &&
                   SpriteProjectionFactory.GetSpriteRenderer(root).gameObject.activeSelf;
        }

        public bool Apply(SpriteRoleTag tag)
        {
            if (tag == null)
            {
                return false;
            }
            EnsureDependencies();
            if (!projectionEnabled)
            {
                SpriteProjectionFactory.Disable(tag.gameObject);
                return false;
            }
            return SpriteProjectionFactory.Enable(tag.gameObject, catalog, targetCamera, style);
        }

        public int ApplyAllTagged()
        {
            SpriteRoleTag[] tags = FindObjectsByType<SpriteRoleTag>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            Array.Sort(tags, CompareTags);
            int applied = 0;
            for (int i = 0; i < tags.Length; i++)
            {
                if (tags[i] != null)
                {
                    tags[i].SetDeterministicSortIndex(i);
                }
                if (Apply(tags[i]))
                {
                    applied++;
                }
            }
            ApplyObstacleFootprints(projectionEnabled);
            RebuildVisualCache();
            RefreshSortOrder();
            return applied;
        }

        public void SetProjectionEnabled(bool enabled)
        {
            projectionEnabled = enabled;
            SpriteRoleTag[] tags = FindObjectsByType<SpriteRoleTag>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < tags.Length; i++)
            {
                if (tags[i] == null)
                {
                    continue;
                }
                if (enabled)
                {
                    Apply(tags[i]);
                }
                else
                {
                    SpriteProjectionFactory.Disable(tags[i].gameObject);
                }
            }
            ApplyObstacleFootprints(enabled);
            if (enabled)
            {
                RebuildVisualCache();
                RefreshSortOrder();
            }
        }

        public static bool RegisterGlobal(
            GameObject root,
            string assetId,
            string role = null,
            string stableId = null)
        {
            if (Instance == null)
            {
                Instance = FindFirstObjectByType<SpriteProjectionBootstrap>(
                    FindObjectsInactive.Include);
            }
            return Instance != null && Instance.Register(root, assetId, role, stableId);
        }

        static void ApplyObstacleFootprints(bool spritesEnabled)
        {
            SpriteProjectionObstacleState[] states = FindObjectsByType<SpriteProjectionObstacleState>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < states.Length; i++)
            {
                states[i]?.Apply(spritesEnabled);
            }
        }

        void RebuildVisualCache()
        {
            sortedVisuals.Clear();
            SpriteProjectionVisual[] visuals = FindObjectsByType<SpriteProjectionVisual>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < visuals.Length; i++)
            {
                if (visuals[i] != null && visuals[i].IsCameraFacing)
                {
                    sortedVisuals.Add(visuals[i]);
                }
            }
        }

        void RefreshSortOrder()
        {
            if (!projectionEnabled || sortedVisuals.Count == 0)
            {
                return;
            }
            sortedVisuals.Sort(CompareVisuals);
            for (int i = 0; i < sortedVisuals.Count; i++)
            {
                sortedVisuals[i]?.SetSortingOrder(i);
            }
        }

        static int CompareVisuals(SpriteProjectionVisual a, SpriteProjectionVisual b)
        {
            if (ReferenceEquals(a, b)) return 0;
            if (a == null) return -1;
            if (b == null) return 1;
            int key = a.SortKey.CompareTo(b.SortKey);
            return key != 0 ? key : a.DeterministicSortIndex.CompareTo(b.DeterministicSortIndex);
        }

        static int CompareTags(SpriteRoleTag a, SpriteRoleTag b)
        {
            if (ReferenceEquals(a, b)) return 0;
            if (a == null) return -1;
            if (b == null) return 1;
            int value = string.CompareOrdinal(a.StableId, b.StableId);
            if (value != 0) return value;
            value = string.CompareOrdinal(a.AssetId, b.AssetId);
            if (value != 0) return value;
            value = string.CompareOrdinal(a.Role, b.Role);
            if (value != 0) return value;
            value = string.CompareOrdinal(a.name, b.name);
            if (value != 0) return value;
            value = a.transform.position.x.CompareTo(b.transform.position.x);
            if (value != 0) return value;
            value = a.transform.position.z.CompareTo(b.transform.position.z);
            if (value != 0) return value;
            return a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex());
        }

        void EnsureDependencies()
        {
            if (style == null)
            {
                style = SpriteProjectionStyle.LoadOrDefault();
            }
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }
        }
    }
}
