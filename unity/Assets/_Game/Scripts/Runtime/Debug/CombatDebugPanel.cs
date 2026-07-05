using System.Text;
using Abbey.Buildings;
using Abbey.Combat;
using Abbey.Core;
using Abbey.Light;
using Abbey.Nightmares;
using UnityEngine;

namespace Abbey.Debugging
{
    /// <summary>
    /// Debug overlay for the light-band combat + two-tier home defense systems
    /// (AGENTS.md "debug overlays for every hidden system"). Toggled with
    /// <see cref="toggleKey"/> (F6, so it coexists with F1-F4 and F7-F9) and drawn on
    /// the right. Surfaces the hidden state: the band-combat multipliers from
    /// <see cref="CombatConfig"/>, the band under each home, and every defended home's
    /// state (sleeping / awake / razed) with its hit-point bar. Display-only: nothing
    /// here holds or tunes a balance value.
    /// </summary>
    [DisallowMultipleComponent]
    public class CombatDebugPanel : MonoBehaviour
    {
        [Tooltip("Key that shows/hides the panel.")]
        public KeyCode toggleKey = KeyCode.F6;

        [Tooltip("Start with the panel visible.")]
        public bool visible = true;

        [Tooltip("Max home rows to list.")]
        [Min(1)] public int maxRows = 12;

        HomeDefenseSystem _defense;
        GUIStyle _labelStyle;
        GUIStyle _headerStyle;
        readonly StringBuilder _bar = new StringBuilder(16);

        HomeDefenseSystem Defense
        {
            get
            {
                if (_defense == null)
                {
                    _defense = HomeDefenseSystem.Instance != null
                        ? HomeDefenseSystem.Instance
                        : FindFirstObjectByType<HomeDefenseSystem>();
                }
                return _defense;
            }
        }

        void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                visible = !visible;
            }
        }

        void OnGUI()
        {
            float width = 340f;
            float x = Screen.width - width - 8f;
            float y = 8f;
            if (!visible)
            {
                GUI.Label(new Rect(x, y, width, 22f), $"[{toggleKey}] combat panel");
                return;
            }

            EnsureStyles();
            GUILayout.BeginArea(new Rect(x, y, width, Mathf.Min(Screen.height - y - 8f, 380f)),
                GUI.skin.box);

            DrawBands();
            DrawHomes();

            GUILayout.EndArea();
        }

        void DrawBands()
        {
            var cfg = CombatConfig.LoadOrDefault();
            Header("Light-band combat (multipliers)");
            GUILayout.Label(
                $"monster  Safe {cfg.safeMonsterDamageMultiplier:0.00}  " +
                $"Edge {cfg.edgeMonsterDamageMultiplier:0.00}  " +
                $"Dark {cfg.darkMonsterDamageMultiplier:0.00}", _labelStyle);
            GUILayout.Label(
                $"friendly Safe {cfg.safeFriendlyDamageMultiplier:0.00}  " +
                $"Edge {cfg.edgeFriendlyDamageMultiplier:0.00}  " +
                $"Dark {cfg.darkFriendlyDamageMultiplier:0.00}", _labelStyle);
            GUILayout.Label(
                $"dark sanity drain/s {cfg.darkFriendlySanityDrainPerSecond:0.000}   " +
                $"(beast: exempt)", _labelStyle);
            GUILayout.Label($"live monsters: {CountLiveMonsters()}", _labelStyle);
        }

        void DrawHomes()
        {
            Header("Home defense");
            var defense = Defense;
            var buildings = Building.Active;
            int rows = 0;
            for (int i = 0; i < buildings.Count && rows < maxRows; i++)
            {
                var b = buildings[i];
                if (b == null || !b.IsDestructibleHome)
                {
                    continue;
                }
                rows++;
                var state = defense != null ? defense.StateOf(b)
                    : (b.IsRazed ? HomeDefenseState.Razed : HomeDefenseState.Sleeping);
                var band = DarknessEvaluator.Classify(b.transform.position);
                GUILayout.Label(
                    $"{b.name}  [{state}]  band={band}  occ={b.Occupants.Count}", _labelStyle);
                GUILayout.Label($"   hp {HpBar(b)} {b.HitPoints:0}/{b.MaxHitPoints:0}", _labelStyle);
            }
            if (rows == 0)
            {
                GUILayout.Label("(no destructible homes placed)", _labelStyle);
            }
        }

        string HpBar(Building b)
        {
            _bar.Length = 0;
            float frac = b.MaxHitPoints > 0f ? Mathf.Clamp01(b.HitPoints / b.MaxHitPoints) : 0f;
            int filled = Mathf.RoundToInt(frac * 10f);
            for (int i = 0; i < 10; i++)
            {
                _bar.Append(i < filled ? '#' : '-');
            }
            return _bar.ToString();
        }

        static int CountLiveMonsters()
        {
            int n = 0;
            var monsters = MonsterController.Active;
            for (int i = 0; i < monsters.Count; i++)
            {
                if (monsters[i] != null && monsters[i].IsAlive)
                {
                    n++;
                }
            }
            return n;
        }

        void EnsureStyles()
        {
            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, richText = false };
            }
            if (_headerStyle == null)
            {
                _headerStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13,
                    fontStyle = FontStyle.Bold
                };
            }
        }

        void Header(string text)
        {
            GUILayout.Space(4f);
            GUILayout.Label(text, _headerStyle);
        }
    }
}
