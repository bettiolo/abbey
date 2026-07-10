using Abbey.Core;
using UnityEngine;
using UnityEngine.Rendering;

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
            int stableSortIndex = tag != null ? tag.DeterministicSortIndex : 0;
            visual.Configure(gameplayRoot.transform, targetCamera, entry, style, stableSortIndex);
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
    internal sealed class SpriteProjectionVisual : MonoBehaviour
    {
        [SerializeField] Transform gameplayRoot;
        [SerializeField] Camera targetCamera;
        [SerializeField] Vector3 anchorOffset;
        [SerializeField] int sortingOffset;
        [SerializeField] int deterministicSortIndex;
        [SerializeField] bool participatesInPhaseTint = true;
        [SerializeField] SpriteProjectionLayout layout;
        [SerializeField] Vector2 localGroundSize = Vector2.one;
        [SerializeField] float localGroundHeight;
        [SerializeField] SpriteProjectionStyle style;
        [SerializeField, Min(0.01f)] float targetWorldScale = 1f;
        [SerializeField] Sprite southSprite;
        [SerializeField] Sprite northSprite;
        [SerializeField] Sprite eastSprite;
        [SerializeField] Sprite westSprite;
        [SerializeField] Sprite[] southWalk;
        [SerializeField] Sprite[] northWalk;
        [SerializeField] Sprite[] eastWalk;
        [SerializeField] Sprite[] westWalk;
        [SerializeField, Min(0.01f)] float walkFrameSeconds = 0.2f;
        [SerializeField] FacingDirection facing = FacingDirection.South;
        Vector3 lastRootPosition;
        float walkElapsed;
        SpriteRenderer spriteRenderer;

        enum FacingDirection
        {
            South,
            North,
            East,
            West
        }

        internal int DeterministicSortIndex => deterministicSortIndex;
        internal bool IsCameraFacing => layout == SpriteProjectionLayout.CameraFacing;
        internal float SortKey
        {
            get
            {
                float depth = targetCamera != null && gameplayRoot != null
                    ? targetCamera.transform.InverseTransformPoint(gameplayRoot.position).z
                    : gameplayRoot != null ? gameplayRoot.position.x + gameplayRoot.position.z : 0f;
                float scale = style != null ? Mathf.Max(0.01f, style.depthSortingScale) : 1f;
                return sortingOffset - depth * scale;
            }
        }

        internal void SetSortingOrder(int order)
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }
            if (spriteRenderer != null)
            {
                spriteRenderer.sortingOrder = order;
            }
        }

        public void Configure(
            Transform gameplayRoot,
            Camera targetCamera,
            SpriteProjectionEntry entry,
            SpriteProjectionStyle projectionStyle,
            int newDeterministicSortIndex)
        {
            this.gameplayRoot = gameplayRoot;
            this.targetCamera = targetCamera;
            anchorOffset = entry.anchorOffset;
            sortingOffset = entry.sortingOffset;
            deterministicSortIndex = newDeterministicSortIndex;
            participatesInPhaseTint = entry.participatesInPhaseTint;
            layout = entry.layout;
            style = projectionStyle != null ? projectionStyle : SpriteProjectionStyle.LoadOrDefault();
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }
            spriteRenderer.sprite = entry.sprite;
            southSprite = entry.southSprite;
            northSprite = entry.northSprite;
            eastSprite = entry.eastSprite;
            westSprite = entry.westSprite;
            southWalk = entry.southWalk;
            northWalk = entry.northWalk;
            eastWalk = entry.eastWalk;
            westWalk = entry.westWalk;
            walkFrameSeconds = Mathf.Max(0.01f, entry.walkFrameSeconds);
            lastRootPosition = gameplayRoot.position;
            walkElapsed = 0f;
            if (entry.HasDirectionalSprites)
            {
                facing = FacingDirection.South;
                spriteRenderer.sprite = southSprite;
            }
            spriteRenderer.shadowCastingMode = ShadowCastingMode.Off;
            spriteRenderer.receiveShadows = false;
            if (layout == SpriteProjectionLayout.GroundTiled)
            {
                ResolveLocalGroundBounds(out localGroundSize, out localGroundHeight);
                spriteRenderer.drawMode = SpriteDrawMode.Tiled;
                spriteRenderer.size = localGroundSize;
                spriteRenderer.sortingOrder = sortingOffset;
            }
            else
            {
                spriteRenderer.drawMode = SpriteDrawMode.Simple;
            }
            targetWorldScale = Mathf.Max(0.01f, entry.visualScale);
            RefreshTransform();
            if (layout == SpriteProjectionLayout.CameraFacing)
            {
                // Isolated factory callers get a depth fallback immediately. In a
                // generated scene the bootstrap replaces this with its dense total order.
                spriteRenderer.sortingOrder = Mathf.Clamp(
                    Mathf.RoundToInt(SortKey), -32000, 32000);
            }
        }

        void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (gameplayRoot == null)
            {
                gameplayRoot = transform.parent;
            }
            if (gameplayRoot != null)
            {
                lastRootPosition = gameplayRoot.position;
            }
            if (style == null)
            {
                style = SpriteProjectionStyle.LoadOrDefault();
            }
        }

        void LateUpdate()
        {
            RefreshActorSprite(Time.deltaTime);
            RefreshTransform();
        }

        void RefreshActorSprite(float deltaTime)
        {
            if (layout != SpriteProjectionLayout.CameraFacing || gameplayRoot == null ||
                southSprite == null || northSprite == null || eastSprite == null || westSprite == null)
            {
                return;
            }

            Vector3 delta = gameplayRoot.position - lastRootPosition;
            lastRootPosition = gameplayRoot.position;
            delta.y = 0f;
            if (delta.sqrMagnitude <= 0.00000001f)
            {
                walkElapsed = 0f;
                spriteRenderer.sprite = IdleSprite(facing);
                return;
            }

            facing = Mathf.Abs(delta.x) >= Mathf.Abs(delta.z)
                ? delta.x >= 0f ? FacingDirection.East : FacingDirection.West
                : delta.z >= 0f ? FacingDirection.North : FacingDirection.South;
            walkElapsed += Mathf.Max(0f, deltaTime);
            Sprite[] frames = WalkFrames(facing);
            if (frames != null && frames.Length > 0)
            {
                int frame = Mathf.FloorToInt(walkElapsed / walkFrameSeconds) % frames.Length;
                spriteRenderer.sprite = frames[frame] != null ? frames[frame] : IdleSprite(facing);
            }
            else
            {
                spriteRenderer.sprite = IdleSprite(facing);
            }
        }

        Sprite IdleSprite(FacingDirection direction)
        {
            switch (direction)
            {
                case FacingDirection.North: return northSprite;
                case FacingDirection.East: return eastSprite;
                case FacingDirection.West: return westSprite;
                default: return southSprite;
            }
        }

        Sprite[] WalkFrames(FacingDirection direction)
        {
            switch (direction)
            {
                case FacingDirection.North: return northWalk;
                case FacingDirection.East: return eastWalk;
                case FacingDirection.West: return westWalk;
                default: return southWalk;
            }
        }

        void RefreshTransform()
        {
            if (layout == SpriteProjectionLayout.GroundTiled)
            {
                transform.localPosition = new Vector3(
                    anchorOffset.x,
                    localGroundHeight + anchorOffset.y + 0.002f,
                    anchorOffset.z);
                transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                transform.localScale = Vector3.one;
                RefreshRendererStyle();
                return;
            }

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
            RefreshRendererStyle();
        }

        void RefreshRendererStyle()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }
            if (spriteRenderer != null && style != null && gameplayRoot != null)
            {
                // The scene bootstrap assigns a dense, total order after comparing
                // continuous projected depth, role offset, and deterministic identity.
                // This local value is a safe fallback for isolated factory use in tests.
                if (layout == SpriteProjectionLayout.GroundTiled)
                {
                    spriteRenderer.sortingOrder = sortingOffset;
                }
                DayPhase phase = GameClock.Instance != null ? GameClock.Instance.Phase : DayPhase.Day;
                spriteRenderer.color = participatesInPhaseTint ? style.TintFor(phase) : Color.white;
            }
        }

        void ResolveLocalGroundBounds(out Vector2 size, out float height)
        {
            size = Vector2.one;
            height = 0f;
            if (gameplayRoot == null)
            {
                return;
            }

            MeshFilter[] filters = gameplayRoot.GetComponentsInChildren<MeshFilter>(true);
            bool found = false;
            Bounds localBounds = default;
            for (int i = 0; i < filters.Length; i++)
            {
                MeshFilter filter = filters[i];
                if (filter == null || filter.sharedMesh == null)
                {
                    continue;
                }
                Bounds meshBounds = filter.sharedMesh.bounds;
                Vector3 min = meshBounds.min;
                Vector3 max = meshBounds.max;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = new Vector3(
                        (corner & 1) == 0 ? min.x : max.x,
                        (corner & 2) == 0 ? min.y : max.y,
                        (corner & 4) == 0 ? min.z : max.z);
                    Vector3 localPoint = gameplayRoot.InverseTransformPoint(
                        filter.transform.TransformPoint(point));
                    if (!found)
                    {
                        localBounds = new Bounds(localPoint, Vector3.zero);
                        found = true;
                    }
                    else
                    {
                        localBounds.Encapsulate(localPoint);
                    }
                }
            }
            if (found)
            {
                size = new Vector2(
                    Mathf.Max(0.01f, localBounds.size.x),
                    Mathf.Max(0.01f, localBounds.size.z));
                height = localBounds.max.y;
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
