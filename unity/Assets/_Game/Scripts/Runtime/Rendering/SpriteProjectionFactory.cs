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
            return Enable(gameplayRoot, entry, targetCamera);
        }

        public static bool Enable(
            GameObject gameplayRoot,
            SpriteProjectionEntry entry,
            Camera targetCamera)
        {
            if (gameplayRoot == null || entry == null || entry.sprite == null)
            {
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
            visual.Configure(gameplayRoot.transform, targetCamera, entry);
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
        Transform _gameplayRoot;
        Camera _targetCamera;
        Vector3 _anchorOffset;
        SpriteRenderer _spriteRenderer;

        public void Configure(
            Transform gameplayRoot,
            Camera targetCamera,
            SpriteProjectionEntry entry)
        {
            _gameplayRoot = gameplayRoot;
            _targetCamera = targetCamera;
            _anchorOffset = entry.anchorOffset;
            if (_spriteRenderer == null)
            {
                _spriteRenderer = GetComponent<SpriteRenderer>();
            }
            _spriteRenderer.sprite = entry.sprite;
            _spriteRenderer.sortingOrder = entry.sortingOffset;
            float scale = Mathf.Max(0.01f, entry.visualScale);
            transform.localScale = new Vector3(scale, scale, 1f);
            RefreshTransform();
        }

        void LateUpdate()
        {
            RefreshTransform();
        }

        void RefreshTransform()
        {
            if (_gameplayRoot != null)
            {
                transform.position = _gameplayRoot.position + _anchorOffset;
            }
            if (_targetCamera != null)
            {
                transform.rotation = _targetCamera.transform.rotation;
            }
        }
    }

    [DisallowMultipleComponent]
    sealed class SpriteLegacyRendererState : MonoBehaviour
    {
        Renderer[] _renderers;
        bool[] _enabledStates;

        public void CaptureIfNeeded(GameObject root)
        {
            if (_renderers != null)
            {
                return;
            }

            MeshRenderer[] meshes = root.GetComponentsInChildren<MeshRenderer>(true);
            SkinnedMeshRenderer[] skinned = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            _renderers = new Renderer[meshes.Length + skinned.Length];
            _enabledStates = new bool[_renderers.Length];

            int target = 0;
            for (int i = 0; i < meshes.Length; i++)
            {
                _renderers[target++] = meshes[i];
            }
            for (int i = 0; i < skinned.Length; i++)
            {
                _renderers[target++] = skinned[i];
            }
            for (int i = 0; i < _renderers.Length; i++)
            {
                _enabledStates[i] = _renderers[i] != null && _renderers[i].enabled;
            }
        }

        public void Hide()
        {
            if (_renderers == null)
            {
                return;
            }
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null)
                {
                    _renderers[i].enabled = false;
                }
            }
        }

        public void Restore()
        {
            if (_renderers == null)
            {
                return;
            }
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null)
                {
                    _renderers[i].enabled = _enabledStates[i];
                }
            }
        }
    }
}
