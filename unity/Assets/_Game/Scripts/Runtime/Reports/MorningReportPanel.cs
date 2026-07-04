using Abbey.Core;
using UnityEngine;

namespace Abbey.Reports
{
    /// <summary>
    /// Minimal storybook presentation of the dawn report (AGENTS.md: a debug/overlay
    /// for every hidden system, but this one is player-facing). It listens for
    /// <see cref="MorningReportSystem.ReportReady"/> and shows itself automatically at
    /// dawn — a dark vignette, the storybook prose, and a terse stat block — then hides
    /// when the new day begins. F5 toggles a manual view of the last report (F1
    /// DebugOverlay, F2 Economy, F3 Buildings, F4 Nightmare are taken). Draw only; it
    /// never tunes a value.
    /// </summary>
    [DisallowMultipleComponent]
    public class MorningReportPanel : MonoBehaviour
    {
        [Tooltip("Key that shows/hides the last report on demand.")]
        public KeyCode toggleKey = KeyCode.F5;

        [Tooltip("Automatically reveal the panel when a report is published at dawn.")]
        public bool autoShowAtDawn = true;

        bool _visible;
        bool _haveReport;
        string _prose = string.Empty;
        MorningReportData _data;

        Texture2D _vignette;
        GUIStyle _proseStyle;
        GUIStyle _statStyle;
        GUIStyle _titleStyle;

        void OnEnable()
        {
            MorningReportSystem.ReportReady += OnReportReady;
            EventBus.PhaseChanged += OnPhaseChanged;
        }

        void OnDisable()
        {
            MorningReportSystem.ReportReady -= OnReportReady;
            EventBus.PhaseChanged -= OnPhaseChanged;
        }

        void OnReportReady(MorningReportData data, string prose)
        {
            _data = data;
            _prose = prose;
            _haveReport = true;
            if (autoShowAtDawn)
            {
                _visible = true;
            }
        }

        void OnPhaseChanged(DayPhase phase)
        {
            // The book closes when the world wakes into a new working day.
            if (phase == DayPhase.Day)
            {
                _visible = false;
            }
        }

        void Update()
        {
            if (Input.GetKeyDown(toggleKey) && _haveReport)
            {
                _visible = !_visible;
            }
        }

        void OnGUI()
        {
            if (!_visible)
            {
                if (_haveReport)
                {
                    GUI.Label(new Rect(8f, Screen.height - 30f, 320f, 22f),
                        $"[{toggleKey}] morning report");
                }
                return;
            }

            EnsureStyles();

            // Dark vignette over the whole screen.
            GUI.color = new Color(1f, 1f, 1f, 1f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _vignette);

            float width = Mathf.Min(720f, Screen.width - 80f);
            float height = Mathf.Min(520f, Screen.height - 80f);
            float x = (Screen.width - width) * 0.5f;
            float y = (Screen.height - height) * 0.5f;

            GUILayout.BeginArea(new Rect(x, y, width, height), GUI.skin.box);

            GUILayout.Label(_data.NightNumber > 0
                ? $"— After the {Ordinal(_data.NightNumber)} Night —"
                : "— The Morning After —", _titleStyle);
            GUILayout.Space(10f);

            GUILayout.Label(_prose, _proseStyle);
            GUILayout.Space(14f);

            GUILayout.Label(StatBlock(_data), _statStyle);

            GUILayout.FlexibleSpace();
            GUILayout.Label($"[{toggleKey}] close", _statStyle);
            GUILayout.EndArea();
        }

        static string StatBlock(MorningReportData d)
        {
            string hound = string.IsNullOrEmpty(d.HoundDisposition) ? "—" : d.HoundDisposition;
            return
                $"survivors {Show(d.Survivors)}   dead {d.Dead}   missing {d.Missing}   " +
                $"injured {d.Injured}   rescued {d.Rescued}\n" +
                $"fires lost {d.FiresLost}   relit {d.FiresRelit}   " +
                $"food used {d.FoodConsumed}   wood burned {d.WoodConsumed}   oil {d.OilConsumed}\n" +
                $"hound: {hound} (trust {Dir(d.HoundTrustDirection)}, fear {Dir(d.HoundFearDirection)})   " +
                $"bell rung {d.BellRangCount}   panic {d.PanicEvents}";
        }

        static string Show(int v) => v < 0 ? "—" : v.ToString();

        static string Dir(int d) => d > 0 ? "rose" : d < 0 ? "fell" : "steady";

        static string Ordinal(int n)
        {
            switch (n)
            {
                case 1: return "First";
                case 2: return "Second";
                case 3: return "Third";
                case 4: return "Fourth";
                case 5: return "Fifth";
                default: return n + "th";
            }
        }

        void EnsureStyles()
        {
            if (_vignette == null)
            {
                _vignette = new Texture2D(1, 1);
                _vignette.SetPixel(0, 0, new Color(0.02f, 0.02f, 0.04f, 0.82f));
                _vignette.Apply();
            }
            if (_titleStyle == null)
            {
                _titleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 20,
                    fontStyle = FontStyle.BoldAndItalic,
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = true
                };
            }
            if (_proseStyle == null)
            {
                _proseStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 16,
                    wordWrap = true,
                    richText = false
                };
                _proseStyle.padding = new RectOffset(12, 12, 8, 8);
            }
            if (_statStyle == null)
            {
                _statStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12,
                    wordWrap = true
                };
            }
        }
    }
}
