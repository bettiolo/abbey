using Abbey.Beast;
using Abbey.Core;
using Abbey.Hero;
using Abbey.Light;
using Abbey.Nightmares;
using Abbey.Session;
using Abbey.Villagers;
using Abbey.World;
using UnityEngine;

namespace Abbey.Debugging
{
    /// <summary>
    /// The AGENTS.md "debug overlay for every hidden system" rule, in one OnGUI
    /// panel. Toggled with <see cref="toggleKey"/> (F1). Shows: clock (day/phase/
    /// progress), every villager's state + zone + fear, the hound's bond values,
    /// live monsters, the nightmare director, hero vitals and the resource stub
    /// (carried food + light fuel). Display-only: every number comes from the
    /// live systems; nothing here holds or tunes a balance value.
    /// </summary>
    [DisallowMultipleComponent]
    public class DebugOverlay : MonoBehaviour
    {
        [Tooltip("Key that shows/hides the overlay.")]
        public KeyCode toggleKey = KeyCode.F1;

        [Tooltip("Start with the overlay visible.")]
        public bool visible;

        [Tooltip("Seconds between re-scans for scene singletons (hound, hero, director).")]
        [Min(0.1f)] public float rescanIntervalSeconds = 2f;

        HoundController _hound;
        HoundEvolutionSystem _evolution;
        BellkeeperController _hero;
        NightmareDirector _director;
        float _rescanTimer;
        GUIStyle _labelStyle;
        GUIStyle _headerStyle;
        Vector2 _scroll;

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
                Rescan();
            }
        }

        /// <summary>Finds scene singletons with the Unity 6 lookup APIs.</summary>
        public void Rescan()
        {
            if (_hound == null)
            {
                _hound = FindFirstObjectByType<HoundController>();
            }
            if (_evolution == null)
            {
                _evolution = FindFirstObjectByType<HoundEvolutionSystem>();
            }
            if (_hero == null)
            {
                _hero = FindFirstObjectByType<BellkeeperController>();
            }
            if (_director == null)
            {
                _director = FindFirstObjectByType<NightmareDirector>();
            }
        }

        void OnGUI()
        {
            if (!visible)
            {
                GUI.Label(new Rect(8f, 74f, 220f, 22f), $"[{toggleKey}] debug overlay");
                return;
            }

            EnsureStyles();

            float width = 420f;
            float height = Mathf.Min(Screen.height - 16f, 640f);
            GUILayout.BeginArea(new Rect(8f, 8f, width, height), GUI.skin.box);
            _scroll = GUILayout.BeginScrollView(_scroll);

            DrawClock();
            DrawCampaign();
            DrawHero();
            DrawHound();
            DrawMonstersAndDirector();
            DrawLights();
            DrawVillagers();

            GUILayout.EndScrollView();
            GUILayout.EndArea();
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

        void Line(string text)
        {
            GUILayout.Label(text, _labelStyle);
        }

        void DrawClock()
        {
            var clock = GameClock.Instance;
            Header($"Clock   [{toggleKey}] hide");
            if (clock == null)
            {
                Line("no GameClock in scene");
                return;
            }
            Line($"Day {clock.DayNumber} — {clock.Phase}  " +
                 $"{clock.PhaseProgress * 100f:F0}% " +
                 $"({clock.TimeInPhase:F1}s / {clock.GetPhaseDuration(clock.Phase):F0}s)  " +
                 $"t={clock.TotalTime:F1}s");
        }

        void DrawCampaign()
        {
            var chapters = ChapterSystem.Instance;
            var ship = SpringShipScenario.Instance;
            if (chapters == null && ship == null)
            {
                return; // Phase 2 slice: no campaign systems in the scene.
            }

            Header("Campaign (P3-14)");
            var season = SeasonSystem.Instance;
            string yearSeason = season != null
                ? $"year {season.YearNumber} — {season.CurrentSeason} (day-of-year {season.DayOfYear})"
                : "no SeasonSystem";
            if (chapters != null)
            {
                Line($"chapter {chapters.CurrentChapterIndex + 1}/4: {chapters.CurrentChapterName}   {yearSeason}");
            }
            else
            {
                Line(yearSeason);
            }

            if (ship != null)
            {
                var m = ship.EvaluateManifest();
                Line($"manifest: settlers {Mark(m.SettlersReady)} ({m.WillingSailors}/{m.SettlersRequired})  " +
                     $"provisions {Mark(m.ProvisionsReady)}  hull {Mark(m.HullReady)}  " +
                     $"=> {(m.Complete ? "COMPLETE" : "incomplete")}");
                Line($"launch window: {(ship.LaunchWindowOpen ? "OPEN" : "closed")}  " +
                     $"sailed={(ship.HasSailed ? "yes" : "no")}");
            }
        }

        static string Mark(bool ready) => ready ? "OK" : "--";

        void DrawHero()
        {
            Header("Bellkeeper / resources (stub)");
            if (_hero == null)
            {
                Line("no BellkeeperController in scene");
                return;
            }
            Line($"health={_hero.Health:F0}  stamina={_hero.Stamina:F0}  " +
                 $"food={_hero.CarriedFood}  flame={(_hero.IsCarryingFlame ? "LIT" : "out")}  " +
                 $"escorting={(_hero.EscortedVillager != null ? _hero.EscortedVillager.name : "-")}");
            Line($"zone={DarknessEvaluator.Classify(_hero.transform.position)}  " +
                 $"pos={FormatPos(_hero.transform.position)}");
        }

        void DrawHound()
        {
            Header("Black Hound");
            if (_hound == null)
            {
                Line("no HoundController in scene");
                return;
            }
            Line($"state={_hound.State}  starving={_hound.IsStarving}  " +
                 $"bellTarget={(_hound.HasBellTarget ? "yes" : "no")}  " +
                 $"engaged={(_hound.EngagedMonster != null ? _hound.EngagedMonster.name : "-")}");
            Line($"trust={_hound.Trust:F2}  hunger={_hound.Hunger:F2}  pain={_hound.Pain:F2}  " +
                 $"fear={_hound.Fear:F2}  attach={_hound.Attachment:F2}");
            Line($"treatment: feed={_hound.FeedEvents} allied={_hound.AlliedFights} " +
                 $"solo={_hound.SoloHunts} rites={_hound.Rites} " +
                 $"chain={_hound.ChainSeconds:F0}s injuries={_hound.Injuries}");
            DrawEvolution();
        }

        void DrawEvolution()
        {
            if (_evolution == null)
            {
                Line("evolution: no HoundEvolutionSystem in scene");
                return;
            }
            Line($"PATH={_evolution.CurrentPath}{(_evolution.PathLocked ? " (LOCKED)" : "")}  " +
                 $"doctrine={_evolution.Doctrine}  beastStatus={_evolution.BeastStatus:F2}");
            Line($"scores  Guard={_evolution.ScoreFor(HoundPath.Guardian):F1} " +
                 $"War={_evolution.ScoreFor(HoundPath.War):F1} " +
                 $"Starv={_evolution.ScoreFor(HoundPath.Starved):F1} " +
                 $"Sacr={_evolution.ScoreFor(HoundPath.Sacred):F1} " +
                 $"Brok={_evolution.ScoreFor(HoundPath.Broken):F1}  " +
                 $"(adopt≥{_evolution.LastDominantScore:F1})");
        }

        void DrawMonstersAndDirector()
        {
            var monsters = MonsterController.Active;
            Header($"Monsters ({monsters.Count} active)");
            for (int i = 0; i < monsters.Count; i++)
            {
                var m = monsters[i];
                if (m == null)
                {
                    continue;
                }
                Line($"{m.name}  hp={m.Health:F0}  fleeing={m.IsFleeing}  " +
                     $"zone={DarknessEvaluator.Classify(m.transform.position)}");
            }

            Header("Nightmare director");
            if (_director == null)
            {
                Line("no NightmareDirector in scene");
                return;
            }
            Line($"spawned this night={_director.SpawnedMonsters.Count}  " +
                 $"log records={GameEventLog.Count}");
        }

        void DrawLights()
        {
            var sources = DarknessEvaluator.Sources;
            int lit = 0;
            for (int i = 0; i < sources.Count; i++)
            {
                if (sources[i] != null && sources[i].isLit)
                {
                    lit++;
                }
            }
            Header($"Lights ({lit} lit / {sources.Count})");
            for (int i = 0; i < sources.Count; i++)
            {
                var s = sources[i];
                if (s == null)
                {
                    continue;
                }
                string fuel = s.HasInfiniteFuel ? "inf" : $"{s.fuelSeconds:F0}s";
                Line($"{s.name}  {(s.isLit ? "lit" : "OUT")}  r={s.EffectiveRadius:F1}  " +
                     $"fuel={fuel}{(s.sacred ? "  SACRED" : "")}");
            }
        }

        void DrawVillagers()
        {
            var villagers = DuskRecallSystem.Villagers;
            Header($"Villagers ({villagers.Count})");
            for (int i = 0; i < villagers.Count; i++)
            {
                var v = villagers[i];
                if (v == null)
                {
                    continue;
                }
                Line($"{v.name}  {v.State}  zone={v.CurrentZone}  fear={v.Fear:F2}  " +
                     $"brave={v.Bravery:F2}{(v.IsEscorted ? "  ESCORTED" : "")}" +
                     $"{(v.IsRecallOrdered ? "  recall" : "")}");
            }
        }

        static string FormatPos(Vector3 p)
        {
            return $"({p.x:F1}, {p.z:F1})";
        }
    }
}
