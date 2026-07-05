using Abbey.Core;
using Abbey.Economy;
using Abbey.Hero;
using Abbey.Nightmares;
using Abbey.Villagers;
using UnityEngine;

namespace Abbey.UI
{
    /// <summary>
    /// The player-facing HUD (unlike the F1–F4 debug panels, this one ships).
    /// Top-center strip: day/phase clock with a phase-progress bar, the settlement
    /// stockpile, and the villager headcount (plus live monsters when any are up).
    /// Bottom-center strip: bellkeeper vitals — health and stamina bars, carried
    /// food, and the carried-flame state. F7 shows/hides it. Display-only: every
    /// number is read live from GameClock, ResourceLedger, DuskRecallSystem,
    /// MonsterController and the BellkeeperController; nothing here holds or tunes
    /// a balance value.
    /// </summary>
    [DisallowMultipleComponent]
    public class GameHud : MonoBehaviour
    {
        [Tooltip("Key that shows/hides the HUD.")]
        public KeyCode toggleKey = KeyCode.F7;

        [Tooltip("Start with the HUD visible.")]
        public bool visible = true;

        [Tooltip("Seconds between re-scans for the scene hero.")]
        [Min(0.1f)] public float rescanIntervalSeconds = 2f;

        BellkeeperController _hero;
        float _rescanTimer;
        Texture2D _solid;
        GUIStyle _labelStyle;
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
            }
        }

        void OnDestroy()
        {
            if (_solid != null)
            {
                Destroy(_solid);
            }
        }

        // ------------------------------------------------------------------
        // Pure formatting helpers (EditMode-tested)
        // ------------------------------------------------------------------

        /// <summary>"Day 2 — Dusk 42%". Progress is clamped to 0..1.</summary>
        public static string FormatClock(int dayNumber, DayPhase phase, float phaseProgress)
        {
            return $"Day {dayNumber} — {phase} {Mathf.Clamp01(phaseProgress) * 100f:F0}%";
        }

        /// <summary>The stockpile strip, matching the ledger's integer units.</summary>
        public static string FormatStockLine(
            int wood, int food, int oil, int medicine, int totalStored, int capacity)
        {
            return $"Wood {wood}   Food {food}   Oil {oil}   Medicine {medicine}   " +
                   $"Stored {totalStored}/{capacity}";
        }

        /// <summary>Dead and Missing villagers no longer count toward the headcount.</summary>
        public static bool CountsAsLiving(VillagerState state)
        {
            return state != VillagerState.Dead && state != VillagerState.Missing;
        }

        // ------------------------------------------------------------------
        // Drawing
        // ------------------------------------------------------------------

        void OnGUI()
        {
            if (!visible)
            {
                GUI.Label(new Rect(Mathf.Max(8f, Screen.width * 0.5f - 40f), 8f, 200f, 22f),
                    $"[{toggleKey}] HUD");
                return;
            }

            EnsureStyles();
            DrawTopStrip();
            DrawHeroStrip();
        }

        void DrawTopStrip()
        {
            float width = Mathf.Min(660f, Mathf.Max(280f, Screen.width - 16f));
            float x = Mathf.Max(8f, (Screen.width - width) * 0.5f);
            var strip = new Rect(x, 8f, width, 58f);
            GUI.Box(strip, GUIContent.none);

            var clock = GameClock.Instance;
            string clockText = clock != null
                ? FormatClock(clock.DayNumber, clock.Phase, clock.PhaseProgress)
                : "no clock";

            var villagers = DuskRecallSystem.Villagers;
            int living = 0;
            for (int i = 0; i < villagers.Count; i++)
            {
                if (villagers[i] != null && CountsAsLiving(villagers[i].State))
                {
                    living++;
                }
            }
            string headcount = $"Villagers {living}/{villagers.Count}";

            int monsters = 0;
            var active = MonsterController.Active;
            for (int i = 0; i < active.Count; i++)
            {
                if (active[i] != null && active[i].IsAlive)
                {
                    monsters++;
                }
            }
            if (monsters > 0)
            {
                headcount += $"   Monsters {monsters}";
            }

            float headcountWidth = Mathf.Min(250f, strip.width * 0.42f);
            GUI.Label(new Rect(strip.x + 10f, strip.y + 4f,
                    strip.width - headcountWidth - 24f, 20f),
                clockText, _labelStyle);
            GUI.Label(new Rect(strip.x + strip.width - headcountWidth - 10f, strip.y + 4f,
                    headcountWidth, 20f),
                headcount, _labelStyle);

            // Thin phase-progress bar under the clock text.
            if (clock != null)
            {
                DrawBar(new Rect(strip.x + 10f, strip.y + 24f,
                        Mathf.Min(180f, strip.width - 20f), 5f),
                    clock.PhaseProgress, PhaseBarColor(clock.Phase));
            }

            GUI.Label(new Rect(strip.x + 10f, strip.y + 34f, strip.width - 20f, 20f),
                FormatStockLine(
                    ResourceLedger.Get(ResourceType.Wood),
                    ResourceLedger.Get(ResourceType.Food),
                    ResourceLedger.Get(ResourceType.Oil),
                    ResourceLedger.Get(ResourceType.Medicine),
                    ResourceLedger.TotalStored,
                    ResourceLedger.Capacity),
                _labelStyle);
        }

        void DrawHeroStrip()
        {
            if (_hero == null)
            {
                return;
            }

            float width = Mathf.Min(480f, Mathf.Max(280f, Screen.width - 16f));
            float height = 30f;
            var strip = new Rect(Mathf.Max(8f, (Screen.width - width) * 0.5f),
                Screen.height - height - 8f, width, height);
            GUI.Box(strip, GUIContent.none);

            var cfg = _hero.Config;
            float y = strip.y + 8f;
            float barWidth = Mathf.Min(110f, (strip.width - 188f) * 0.5f);
            barWidth = Mathf.Max(50f, barWidth);
            GUI.Label(new Rect(strip.x + 10f, strip.y + 5f, 24f, 20f), "HP", _labelStyle);
            DrawBar(new Rect(strip.x + 36f, y, barWidth, 12f),
                cfg.bellkeeperMaxHealth > 0f ? _hero.Health / cfg.bellkeeperMaxHealth : 0f,
                new Color(0.85f, 0.30f, 0.25f));
            float staminaX = strip.x + 46f + barWidth;
            GUI.Label(new Rect(staminaX, strip.y + 5f, 24f, 20f), "ST", _labelStyle);
            DrawBar(new Rect(staminaX + 26f, y, barWidth, 12f),
                cfg.bellkeeperMaxStamina > 0f ? _hero.Stamina / cfg.bellkeeperMaxStamina : 0f,
                new Color(0.35f, 0.65f, 0.85f));
            float textX = staminaX + 36f + barWidth;
            GUI.Label(new Rect(textX, strip.y + 5f, strip.xMax - textX - 8f, 20f),
                $"Food x{_hero.CarriedFood}   Flame {(_hero.IsCarryingFlame ? "LIT" : "out")}",
                _labelStyle);
        }

        void DrawBar(Rect rect, float fraction, Color fill)
        {
            var previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(rect, _solid);
            GUI.color = fill;
            GUI.DrawTexture(new Rect(rect.x + 1f, rect.y + 1f,
                (rect.width - 2f) * Mathf.Clamp01(fraction), rect.height - 2f), _solid);
            GUI.color = previous;
        }

        static Color PhaseBarColor(DayPhase phase)
        {
            switch (phase)
            {
                case DayPhase.Day: return new Color(0.95f, 0.85f, 0.45f);
                case DayPhase.Dusk: return new Color(0.90f, 0.55f, 0.30f);
                case DayPhase.Night: return new Color(0.35f, 0.40f, 0.75f);
                case DayPhase.Dawn: return new Color(0.80f, 0.65f, 0.70f);
                default: return Color.white;
            }
        }

        void EnsureStyles()
        {
            if (_solid == null)
            {
                _solid = new Texture2D(1, 1);
                _solid.SetPixel(0, 0, Color.white);
                _solid.Apply();
            }
            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, richText = false };
            }
        }
    }
}
