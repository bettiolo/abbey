using Abbey.Core;
using UnityEngine;

namespace Abbey.Rendering
{
    /// <summary>
    /// Adds or toggles one sprite child without moving, rotating, scaling, or reparenting
    /// the gameplay root. Legacy mesh renderer enabled states are restored exactly.
    /// </summary>
    public static class SpriteProjectionFactory
    {
        public const string VisualChildName = "SpriteProjectionVisual";

        public static bool Enable(
            GameObject gameplayRoot,
            SpriteProjectionCatalog catalog,
            Camera targetCamera)
        {
            return Enable(gameplayRoot, catalog, targetCamera, null);
        }

        public static bool Enable(
            GameObject gameplayRoot,
            SpriteProjectionCatalog catalog,
            Camera targetCamera,
            SpriteProjectionStyle style)
        {
            if (gameplayRoot == null || catalog == null)
            {
                return false;
            }

            SpriteRoleTag tag = gameplayRoot.GetComponent<SpriteRoleTag>();
            if (!catalog.TryGet(tag, out SpriteProjectionEntry entry))
            {
                Disable(gameplayRoot);
                return false;
            }
            return Enable(gameplayRoot, entry, targetCamera, style);
        }

        public static bool Enable(
            GameObject gameplayRoot,
            SpriteProjectionEntry entry,
            Camera targetCamera)
        {
            return Enable(gameplayRoot, entry, targetCamera, null);
        }

        public static bool Enable(
            GameObject gameplayRoot,
            SpriteProjectionEntry entry,
            Camera targetCamera,
            SpriteProjectionStyle style)
        {
            if (gameplayRoot == null || entry == null || entry.sprite == null)
            {
                Disable(gameplayRoot);
                return false;
            }

            SpriteLegacyRendererState legacy = gameplayRoot.GetComponent<SpriteLegacyRendererState>();
            if (legacy == null)
            {
                legacy = gameplayRoot.AddComponent<SpriteLegacyRendererState>();
            }
            legacy.CaptureIfNeeded(gameplayRoot);
            legacy.Hide();

            SpriteProjectionVisual visual = FindVisual(gameplayRoot);
            if (visual == null)
            {
                var child = new GameObject(VisualChildName);
                child.transform.SetParent(gameplayRoot.transform, false);
                visual = child.AddComponent<SpriteProjectionVisual>();
            }

            visual.gameObject.SetActive(true);
            SpriteRoleTag tag = gameplayRoot.GetComponent<SpriteRoleTag>();
            int stableSortKey = tag != null ? tag.StableSortKey : 0;
            visual.Configure(gameplayRoot.transform, targetCamera, entry, style, stableSortKey);
            return true;
        }

        public static void Disable(GameObject gameplayRoot)
        {
            if (gameplayRoot == null)
            {
                return;
            }

            SpriteProjectionVisual visual = FindVisual(gameplayRoot);
            if (visual != null)
            {
                visual.gameObject.SetActive(false);
            }

            SpriteLegacyRendererState legacy = gameplayRoot.GetComponent<SpriteLegacyRendererState>();
            if (legacy != null)
            {
                legacy.Restore();
            }
        }

        public static SpriteRenderer GetSpriteRenderer(GameObject gameplayRoot)
        {
            SpriteProjectionVisual visual = FindVisual(gameplayRoot);
            return visual != null ? visual.GetComponent<SpriteRenderer>() : null;
        }

        static SpriteProjectionVisual FindVisual(GameObject root)
        {
            Transform child = root.transform.Find(VisualChildName);
            return child != null ? child.GetComponent<SpriteProjectionVisual>() : null;
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    sealed class SpriteProjectionVisual : MonoBehaviour
    {
        [SerializeField] Transform gameplayRoot;
        [SerializeField] Camera targetCamera;
        [SerializeField] Vector3 anchorOffset;
        [SerializeField] int sortingOffset;
        [SerializeField] int stableSortKey;
        [SerializeField] bool participatesInPhaseTint = true;
        [SerializeField] SpriteProjectionStyle style;
        [SerializeField, Min(0.01f)] float targetWorldScale = 1f;
        SpriteRenderer spriteRenderer;

        public void Configure(
            Transform gameplayRoot,
            Camera targetCamera,
            SpriteProjectionEntry entry,
            SpriteProjectionStyle projectionStyle,
            int newStableSortKey)
        {
            this.gameplayRoot = gameplayRoot;
            this.targetCamera = targetCamera;
            anchorOffset = entry.anchorOffset;
            sortingOffset = entry.sortingOffset;
            stableSortKey = newStableSortKey;
            participatesInPhaseTint = entry.participatesInPhaseTint;
            style = projectionStyle != null ? projectionStyle : SpriteProjectionStyle.LoadOrDefault();
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }
            spriteRenderer.sprite = entry.sprite;
            targetWorldScale = Mathf.Max(0.01f, entry.visualScale);
            RefreshTransform();
        }

        void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (gameplayRoot == null)
            {
                gameplayRoot = transform.parent;
            }
            if (style == null)
            {
                style = SpriteProjectionStyle.LoadOrDefault();
            }
        }

        void LateUpdate()
        {
            RefreshTransform();
        }

        void RefreshTransform()
        {
            if (gameplayRoot != null)
            {
                transform.position = gameplayRoot.position + anchorOffset;
            }
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }
            if (targetCamera != null)
            {
                transform.rotation = targetCamera.transform.rotation;
            }
            SetWorldScale();
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }
            if (spriteRenderer != null && style != null && gameplayRoot != null)
            {
                float depth = targetCamera != null
                    ? targetCamera.transform.InverseTransformPoint(gameplayRoot.position).z
                    : gameplayRoot.position.x + gameplayRoot.position.z;
                int tie = (stableSortKey & int.MaxValue) % Mathf.Max(1, style.stableTieBreakRange);
                spriteRenderer.sortingOrder = sortingOffset
                    - Mathf.RoundToInt(depth * Mathf.Max(0.01f, style.depthSortingScale)) + tie;
                DayPhase phase = GameClock.Instance != null ? GameClock.Instance.Phase : DayPhase.Day;
                spriteRenderer.color = participatesInPhaseTint ? style.TintFor(phase) : Color.white;
            }
        }

        void SetWorldScale()
        {
            if (gameplayRoot == null)
            {
                transform.localScale = new Vector3(targetWorldScale, targetWorldScale, 1f);
                return;
            }
            for (int i = 0; i < 3; i++)
            {
                Vector3 current = transform.lossyScale;
                Vector3 local = transform.localScale;
                transform.localScale = new Vector3(
                    local.x * targetWorldScale / Mathf.Max(0.0001f, Mathf.Abs(current.x)),
                    local.y * targetWorldScale / Mathf.Max(0.0001f, Mathf.Abs(current.y)),
                    local.z / Mathf.Max(0.0001f, Mathf.Abs(current.z)));
            }
        }
    }

    [DisallowMultipleComponent]
    sealed class SpriteLegacyRendererState : MonoBehaviour
    {
        [SerializeField] Renderer[] renderers;
        [SerializeField] bool[] enabledStates;

        public void CaptureIfNeeded(GameObject root)
        {
            if (renderers != null)
            {
                return;
            }

            MeshRenderer[] meshes = root.GetComponentsInChildren<MeshRenderer>(true);
            SkinnedMeshRenderer[] skinned = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            renderers = new Renderer[meshes.Length + skinned.Length];
            enabledStates = new bool[renderers.Length];

            int target = 0;
            for (int i = 0; i < meshes.Length; i++)
            {
                renderers[target++] = meshes[i];
            }
            for (int i = 0; i < skinned.Length; i++)
            {
                renderers[target++] = skinned[i];
            }
            for (int i = 0; i < renderers.Length; i++)
            {
                enabledStates[i] = renderers[i] != null && renderers[i].enabled;
            }
        }

        public void Hide()
        {
            if (renderers == null)
            {
                return;
            }
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].enabled = false;
                }
            }
        }

        public void Restore()
        {
            if (renderers == null)
            {
                return;
            }
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].enabled = enabledStates[i];
                }
            }
        }
    }
}
