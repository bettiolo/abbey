using Abbey.Beast;
using Abbey.Core;
using Abbey.Hero;
using Abbey.Light;
using Abbey.Nightmares;
using Abbey.Villagers;
using UnityEngine;

namespace Abbey.UI
{
    /// <summary>
    /// Player-facing top-right minimap, axis-aligned (world +Z is up). The
    /// background is a small texture rebuilt every <see cref="redrawIntervalSeconds"/>:
    /// ground tinted by the current day phase, with the light-territory zones
    /// painted from <see cref="DarknessEvaluator.Classify"/> — light IS territory,
    /// so the map shows exactly what the simulation believes. Live markers are
    /// drawn every frame on top: lit lights (sacred flame gold), villagers
    /// (panicking yellow, injured orange), monsters red, the hound violet, the
    /// bellkeeper white. F8 shows/hides it. Display-only; it never tunes a value —
    /// the sizes below are screen layout, not game balance.
    /// </summary>
    [DisallowMultipleComponent]
    public class MinimapPanel : MonoBehaviour
    {
        [Tooltip("Key that shows/hides the minimap.")]
        public KeyCode toggleKey = KeyCode.F8;

        [Tooltip("Start with the minimap visible.")]
        public bool visible = true;

        [Tooltip("On-screen size of the square map, in pixels.")]
        [Min(32)] public int mapSizePixels = 176;

        [Tooltip("Background texture resolution (square). Higher = crisper zones, slower rebuild.")]
        [Min(16)] public int textureResolution = 96;

        [Tooltip("Seconds between background rebuilds (zones move slowly; markers are per-frame).")]
        [Min(0.05f)] public float redrawIntervalSeconds = 0.5f;

        [Tooltip("Seconds between re-scans for scene singletons (hero, hound).")]
        [Min(0.1f)] public float rescanIntervalSeconds = 2f;

        [Tooltip("World-space XZ minimum shown on the map (matches the camera rig bounds).")]
        public Vector2 worldMin = new Vector2(-50f, -50f);

        [Tooltip("World-space XZ maximum shown on the map.")]
        public Vector2 worldMax = new Vector2(50f, 50f);

        BellkeeperController _hero;
        HoundController _hound;
        Texture2D _background;
        Texture2D _solid;
        Color32[] _pixels;
        float _redrawTimer;
        float _rescanTimer;

        static readonly Color SafeLight = new Color(0.95f, 0.75f, 0.35f);
        static readonly Color HeroColor = Color.white;
        static readonly Color HoundColor = new Color(0.70f, 0.45f, 0.95f);
        static readonly Color MonsterColor = new Color(1.00f, 0.25f, 0.20f);
        static readonly Color VillagerColor = new Color(0.40f, 0.90f, 0.40f);
        static readonly Color PanickingColor = new Color(1.00f, 0.90f, 0.20f);
        static readonly Color InjuredColor = new Color(1.00f, 0.55f, 0.10f);
        static readonly Color LightColor = new Color(1.00f, 0.60f, 0.20f);
        static readonly Color SacredColor = new Color(1.00f, 0.85f, 0.30f);

        void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                visible = !visible;
            }

            _rescanTimer -= Time.deltaTime;
            if (_rescanTimer <= 0f)
            {
                _rescanTimer = rescanIntervalSeconds;
                if (_hero == null)
                {
                    _hero = FindAnyObjectByType<BellkeeperController>();
                }
                if (_hound == null)
                {
                    _hound = FindAnyObjectByType<HoundController>();
                }
            }

            _redrawTimer -= Time.deltaTime;
            if (visible && _redrawTimer <= 0f)
            {
                _redrawTimer = redrawIntervalSeconds;
                RebuildBackground();
            }
        }

        void OnDestroy()
        {
            if (_background != null)
            {
                Destroy(_background);
            }
            if (_solid != null)
            {
                Destroy(_solid);
            }
        }

        // ------------------------------------------------------------------
        // Pure projection helpers (EditMode-tested)
        // ------------------------------------------------------------------

        /// <summary>
        /// Projects a world position onto the on-screen map rect. GUI space grows
        /// downward, so world +Z (map north) lands at the TOP of the rect. Positions
        /// outside the world bounds clamp to the map border.
        /// </summary>
        public static Vector2 WorldToMap(Vector3 world, Rect mapRect,
            Vector2 worldMin, Vector2 worldMax)
        {
            float u = Mathf.InverseLerp(worldMin.x, worldMax.x, world.x);
            float v = Mathf.InverseLerp(worldMin.y, worldMax.y, world.z);
            return new Vector2(mapRect.x + u * mapRect.width,
                mapRect.y + (1f - v) * mapRect.height);
        }

        /// <summary>
        /// World position at the center of a background-texture pixel. Texture row 0
        /// is the bottom of the map (IMGUI draws textures upright), so pixel y grows
        /// with world Z — no flip here, unlike <see cref="WorldToMap"/>.
        /// </summary>
        public static Vector3 MapPixelToWorld(int px, int py, int resolution,
            Vector2 worldMin, Vector2 worldMax)
        {
            float u = (px + 0.5f) / resolution;
            float v = (py + 0.5f) / resolution;
            return new Vector3(Mathf.Lerp(worldMin.x, worldMax.x, u), 0f,
                Mathf.Lerp(worldMin.y, worldMax.y, v));
        }

        /// <summary>Base ground tint per day phase (night reads dark, day reads green).</summary>
        public static Color GroundColorFor(DayPhase phase)
        {
            switch (phase)
            {
                case DayPhase.Day: return new Color(0.30f, 0.42f, 0.26f);
                case DayPhase.Dusk: return new Color(0.30f, 0.28f, 0.22f);
                case DayPhase.Night: return new Color(0.07f, 0.08f, 0.13f);
                case DayPhase.Dawn: return new Color(0.26f, 0.24f, 0.27f);
                default: return new Color(0.30f, 0.42f, 0.26f);
            }
        }

        // ------------------------------------------------------------------
        // Background: phase-tinted ground + light territory
        // ------------------------------------------------------------------

        void RebuildBackground()
        {
            int res = textureResolution;
            if (_background == null || _background.width != res)
            {
                if (_background != null)
                {
                    Destroy(_background);
                }
                _background = new Texture2D(res, res, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Bilinear
                };
                _pixels = new Color32[res * res];
            }

            var clock = GameClock.Instance;
            Color ground = GroundColorFor(clock != null ? clock.Phase : DayPhase.Day);
            Color safe = Color.Lerp(ground, SafeLight, 0.60f);
            Color edge = Color.Lerp(ground, SafeLight, 0.28f);

            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    var world = MapPixelToWorld(x, y, res, worldMin, worldMax);
                    Color c;
                    switch (DarknessEvaluator.Classify(world))
                    {
                        case LightZone.Safe: c = safe; break;
                        case LightZone.Edge: c = edge; break;
                        default: c = ground; break;
                    }
                    c.a = 0.92f;
                    _pixels[y * res + x] = c;
                }
            }

            _background.SetPixels32(_pixels);
            _background.Apply(false);
        }

        // ------------------------------------------------------------------
        // Drawing
        // ------------------------------------------------------------------

        void OnGUI()
        {
            if (!visible)
            {
                GUI.Label(new Rect(Mathf.Max(8f, Screen.width - 120f), 8f, 112f, 22f),
                    $"[{toggleKey}] minimap");
                return;
            }

            EnsureSolid();

            float top = Screen.height >= 280 ? 74f : 8f;
            int maxSize = Mathf.Max(32, Mathf.Min(Screen.width - 16, Screen.height - Mathf.RoundToInt(top) - 8));
            int size = Mathf.Clamp(mapSizePixels, 32, maxSize);
            var mapRect = new Rect(Mathf.Max(8f, Screen.width - size - 8f), top,
                size, size);
            GUI.Box(new Rect(mapRect.x - 2f, mapRect.y - 2f,
                mapRect.width + 4f, mapRect.height + 4f), GUIContent.none);
            if (_background != null)
            {
                GUI.DrawTexture(mapRect, _background);
            }

            DrawLightMarkers(mapRect);
            DrawVillagerMarkers(mapRect);
            DrawMonsterMarkers(mapRect);
            if (_hound != null && !_hound.IsMissing)
            {
                DrawMarker(mapRect, _hound.transform.position, 6f, HoundColor);
            }
            if (_hero != null && _hero.IsAlive)
            {
                DrawMarker(mapRect, _hero.transform.position, 6f, HeroColor);
            }
        }

        void DrawLightMarkers(Rect mapRect)
        {
            var sources = DarknessEvaluator.Sources;
            for (int i = 0; i < sources.Count; i++)
            {
                var s = sources[i];
                if (s == null || !s.isLit)
                {
                    continue;
                }
                DrawMarker(mapRect, s.transform.position,
                    s.sacred ? 5f : 4f, s.sacred ? SacredColor : LightColor);
            }
        }

        void DrawVillagerMarkers(Rect mapRect)
        {
            var villagers = DuskRecallSystem.Villagers;
            for (int i = 0; i < villagers.Count; i++)
            {
                var v = villagers[i];
                if (v == null || !GameHud.CountsAsLiving(v.State))
                {
                    continue;
                }
                Color c = VillagerColor;
                if (v.State == VillagerState.Panicking)
                {
                    c = PanickingColor;
                }
                else if (v.State == VillagerState.Injured)
                {
                    c = InjuredColor;
                }
                DrawMarker(mapRect, v.transform.position, 4f, c);
            }
        }

        void DrawMonsterMarkers(Rect mapRect)
        {
            var monsters = MonsterController.Active;
            for (int i = 0; i < monsters.Count; i++)
            {
                var m = monsters[i];
                if (m == null || !m.IsAlive)
                {
                    continue;
                }
                DrawMarker(mapRect, m.transform.position, 5f, MonsterColor);
            }
        }

        void DrawMarker(Rect mapRect, Vector3 worldPos, float size, Color color)
        {
            var p = WorldToMap(worldPos, mapRect, worldMin, worldMax);
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(p.x - size * 0.5f, p.y - size * 0.5f, size, size), _solid);
            GUI.color = previous;
        }

        void EnsureSolid()
        {
            if (_solid == null)
            {
                _solid = new Texture2D(1, 1);
                _solid.SetPixel(0, 0, Color.white);
                _solid.Apply();
            }
        }
    }
}
