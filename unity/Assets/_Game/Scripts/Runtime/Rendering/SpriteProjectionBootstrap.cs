using System;
using UnityEngine;

namespace Abbey.Rendering
{
    /// <summary>Single scene authority for applying and reversing tagged sprite visuals.</summary>
    [DisallowMultipleComponent]
    public sealed class SpriteProjectionBootstrap : MonoBehaviour
    {
        [SerializeField] SpriteProjectionCatalog catalog;
        [SerializeField] SpriteProjectionStyle style;
        [SerializeField] Camera targetCamera;
        [SerializeField] bool projectionEnabled = true;

        public static SpriteProjectionBootstrap Instance { get; private set; }
        public SpriteProjectionCatalog Catalog => catalog;
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

        public void Configure(
            SpriteProjectionCatalog newCatalog,
            Camera camera,
            SpriteProjectionStyle newStyle = null)
        {
            catalog = newCatalog;
            targetCamera = camera;
            style = newStyle;
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
            return Apply(tag);
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
            Array.Sort(tags, (a, b) => string.CompareOrdinal(
                a != null ? a.StableId : string.Empty,
                b != null ? b.StableId : string.Empty));
            int applied = 0;
            for (int i = 0; i < tags.Length; i++)
            {
                if (Apply(tags[i]))
                {
                    applied++;
                }
            }
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
        }

        public static bool RegisterGlobal(
            GameObject root,
            string assetId,
            string role = null,
            string stableId = null)
        {
            return Instance != null && Instance.Register(root, assetId, role, stableId);
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
