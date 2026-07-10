using System.Collections.Generic;
using Abbey.Core;
using Abbey.Nightmares;
using Abbey.UI;
using UnityEngine;

namespace Abbey.Debugging
{
    /// <summary>
    /// Debug panel for the nightmare director (the AGENTS.md "debug overlays for
    /// every hidden system" rule). Toggled with <see cref="toggleKey"/> (F4, so it
    /// coexists with the F1 overlay, the F2 economy panel and F3) and drawn at the
    /// bottom-right. Shows the night script (fired/pending entries), time into
    /// night and countdown to the next event, spawned/alive counts per nightmare
    /// type, the water-death gate, and the recent whisper/panic/nightmare log
    /// tail. P3-11 adds the per-source exploitation-pressure bars
    /// (<see cref="ThreatSourceSystem"/>) and tonight's armed consequence nightmares
    /// (<see cref="ConsequenceNightmareCatalog"/>). Display-only: nothing here holds or
    /// tunes a balance value.
    /// </summary>
    [DisallowMultipleComponent]
    public class NightmareDebugPanel : MonoBehaviour
    {
        [Tooltip("Key that shows/hides the panel.")]
        public KeyCode toggleKey = KeyCode.F4;

        [Tooltip("Start with the panel visible.")]
        public bool visible;

        [Tooltip("Director to display. Unset = first one found in the scene.")]
        public NightmareDirector director;

        [Tooltip("How many trailing whisper/panic/nightmare log records to show.")]
        [Min(0)] public int logTailCount = 8;

        GUIStyle _labelStyle;
        GUIStyle _headerStyle;
        Vector2 _scroll;

        NightmareDirector Director
        {
            get
            {
                if (director == null)
                {
                    director = FindFirstObjectByType<NightmareDirector>();
                }
                return director;
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
            float width = 360f;
            float height = Mathf.Min(Screen.height * 0.5f, 420f);
            float x = Screen.width - width - 8f;
            float y = Screen.height - height - 8f;
            if (!visible)
            {
                HudHintDock.Draw(HudHintSlot.Nightmare, $"[{toggleKey}] nightmare panel");
                return;
            }

            EnsureStyles();

            GUILayout.BeginArea(new Rect(x, y, width, height), GUI.skin.box);
            _scroll = GUILayout.BeginScrollView(_scroll);

            var dir = Director;
            if (dir == null)
            {
                Line("no NightmareDirector in scene");
            }
            else
            {
                DrawDirector(dir);
                DrawSchedule(dir);
                DrawMonsters(dir);
            }
            DrawThreatSources();
            DrawFalseGuidance();
            DrawArmedTriggers();
            DrawLogTail();

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void DrawDirector(NightmareDirector dir)
        {
            Header($"Nightmare director   [{toggleKey}] hide");
            string mode = dir.Config.phase3NightsEnabled
                ? "phase3"
                : (dir.Config.phase2NightsEnabled ? "phase2" : "legacy(0.1)");
            Line($"mode={mode}  night={dir.NightNumber}  active={dir.NightActive}");
            Line($"timeIntoNight={dir.TimeIntoNight:F1}s  " +
                 $"seed={dir.Config.simulationSeed}");
            float? next = dir.SecondsUntilNextEvent;
            Line(next.HasValue
                ? $"next event in {next.Value:F1}s"
                : "next event: none");
            Line($"waterDeathRecorded={NightmareDirector.HasWaterDeathRecord()}  " +
                 $"wreckAnchor={(dir.shipwreckAnchor != null ? dir.shipwreckAnchor.name : "<unset>")}");
        }

        void DrawSchedule(NightmareDirector dir)
        {
            var schedule = dir.Schedule;
            if (schedule == null)
            {
                Header("Schedule: legacy single-spawn night");
                return;
            }
            Header($"Schedule ({schedule.Count} entries)");
            for (int i = 0; i < schedule.Count; i++)
            {
                string mark = i < dir.NextEventIndex ? "x" : " ";
                Line($"[{mark}] {schedule[i]}");
            }
        }

        void DrawMonsters(NightmareDirector dir)
        {
            var types = (NightmareType[])System.Enum.GetValues(typeof(NightmareType));
            int[] spawned = new int[types.Length];
            int[] alive = new int[types.Length];
            var monsters = dir.SpawnedMonsters;
            for (int i = 0; i < monsters.Count; i++)
            {
                var m = monsters[i];
                if (m == null)
                {
                    continue;
                }
                int t = (int)m.Type;
                spawned[t]++;
                if (m.IsAlive)
                {
                    alive[t]++;
                }
            }
            Header($"Nightmares ({monsters.Count} tracked)");
            for (int t = 0; t < types.Length; t++)
            {
                if (spawned[t] == 0)
                {
                    continue;
                }
                Line($"{types[t],-14} spawned={spawned[t]} alive={alive[t]}");
            }
        }

        // ---- P3-11: threat sources + armed consequence triggers ----------

        void DrawThreatSources()
        {
            var threat = ThreatSourceSystem.Instance;
            Header("Threat sources / debt");
            if (threat == null)
            {
                Line("no ThreatSourceSystem in scene");
                return;
            }
            Line($"Forest Debt = {threat.PressureFor(ThreatSourceType.Forest):F2}");
            float max = Mathf.Max(0.01f, threat.Config.maxSourcePressure);
            var sources = threat.Sources;
            var seen = new HashSet<ThreatSourceType>();
            for (int i = 0; i < sources.Count; i++)
            {
                var type = sources[i].Type;
                if (!seen.Add(type))
                {
                    continue; // one bar per source type (several sites share a type's pressure)
                }
                float p = threat.PressureFor(type);
                Line($"{type,-9} {Bar(p / max)} {p:F2}");
            }
            if (sources.Count == 0)
            {
                Line("(no source locations registered)");
            }
        }

        void DrawFalseGuidance()
        {
            Header("False guidance");
            var guidance = FalseGuidanceSystem.Instance;
            if (guidance == null)
            {
                Line("no FalseGuidanceSystem in scene");
                return;
            }
            Line($"fog={guidance.FogActive}  lantern x{Abbey.Light.DarknessEvaluator.MisdirectionLightMultiplier:F2}");
            Line($"false bells={guidance.FalseBellCount}  orders={guidance.FalseGuidanceOrders}");
            Line($"path shifts={guidance.PathShiftCount}  " +
                 $"lastTarget=({guidance.LastFalseLightTarget.x:F1},{guidance.LastFalseLightTarget.z:F1})");
        }

        void DrawArmedTriggers()
        {
            Header("Armed consequence nightmares (tonight)");
            if (Abbey.Decrees.LawSystem.Instance == null)
            {
                Line("no LawSystem — consequences disarmed");
                return;
            }
            var cfg = ThreatConfig.LoadOrDefault();
            var ctx = ConsequenceNightmareCatalog.BuildContext();
            var armed = ConsequenceNightmareCatalog.EvaluateArmed(cfg, ctx);
            if (armed.Count == 0)
            {
                Line("none armed");
                return;
            }
            for (int i = 0; i < armed.Count; i++)
            {
                Line($"{armed[i].type,-14} x{armed[i].spawnCount} @ {armed[i].preferredSource}");
            }
        }

        static string Bar(float fraction01)
        {
            fraction01 = Mathf.Clamp01(fraction01);
            const int width = 12;
            int filled = Mathf.RoundToInt(fraction01 * width);
            return "[" + new string('#', filled) + new string('-', width - filled) + "]";
        }

        void DrawLogTail()
        {
            Header($"Whisper/panic/nightmare log (last {logTailCount})");
            var records = GameEventLog.Records;
            int shown = 0;
            for (int i = records.Count - 1; i >= 0 && shown < logTailCount; i--)
            {
                string type = records[i].Type;
                if (type != "whisper" && type != "panic_event" && type != "nightmare")
                {
                    continue;
                }
                Line(records[i].ToString());
                shown++;
            }
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
    }
}
