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
            GUILayout.BeginArea(new Rect(x, y, width, Mathf.Min(Screen.height - y - 8f, 560f)),
                GUI.skin.box);

            DrawBands();
            DrawHomes();
            DrawEscalation();
            DrawWarriors();

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

        void DrawEscalation()
        {
            Header("Night escalation (P3-06)");
            var esc = NightEscalationSystem.Instance;
            if (esc == null)
            {
                GUILayout.Label("(no escalation system; Phase 3 nights off)", _labelStyle);
                return;
            }
            GUILayout.Label(
                $"night {esc.NightIndex}  season {esc.NightSeason}  " +
                $"{(esc.IsSetPieceTonight ? "SET-PIECE" : "standard")}", _labelStyle);
            GUILayout.Label(
                $"wave budget {esc.TonightWaveBudget:0.0}  -> monsters {esc.TonightMonsterCount}",
                _labelStyle);
            var marker = esc.ActiveMarker;
            if (marker != null)
            {
                string state = marker.IsSolved ? "SOLVED"
                    : (marker.IsRevealed ? "active" : "unrevealed");
                var band = DarknessEvaluator.Classify(marker.transform.position);
                GUILayout.Label(
                    $"objective {marker.Objective.KindId} [{state}] band={band}", _labelStyle);
            }
            else
            {
                GUILayout.Label("objective: (none tonight)", _labelStyle);
            }
        }

        void DrawWarriors()
        {
            Header("Warriors");
            var warriors = WarriorAgent.Active;
            var lodges = WarriorStructure.Active;
            for (int i = 0; i < lodges.Count; i++)
            {
                var s = lodges[i];
                if (s == null)
                {
                    continue;
                }
                if (s.Role == WarriorStructureRole.Lodge)
                {
                    GUILayout.Label(
                        $"{s.name}  lodge  tier {s.AppliedTierCount}/{s.MaxTierCount}  " +
                        $"roster {s.Roster.Count}/{s.EffectiveCapacity}", _labelStyle);
                }
                else
                {
                    GUILayout.Label($"{s.name}  watchtower  vision/support", _labelStyle);
                }
            }
            int live = 0;
            for (int i = 0; i < warriors.Count; i++)
            {
                if (warriors[i] != null && warriors[i].IsAlive)
                {
                    live++;
                }
            }
            GUILayout.Label($"warriors afield: {live}", _labelStyle);
            int shown = 0;
            for (int i = 0; i < warriors.Count && shown < maxRows; i++)
            {
                var w = warriors[i];
                if (w == null)
                {
                    continue;
                }
                shown++;
                var band = DarknessEvaluator.Classify(w.transform.position);
                GUILayout.Label(
                    $"  {w.name} [{w.State}] band={band} hp {w.Health:0}/{w.Stats.MaxHealth:0} " +
                    $"san {w.Sanity:0.00}", _labelStyle);
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
