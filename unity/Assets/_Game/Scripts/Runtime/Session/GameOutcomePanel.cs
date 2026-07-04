using UnityEngine;

namespace Abbey.Session
{
    /// <summary>
    /// Minimal end-screen (AGENTS.md: an overlay for every hidden system; this one is
    /// player-facing). Listens for <see cref="GameSession.OutcomeDecided"/> and shows
    /// itself when the run is decided — the win line, the loss variant, and the
    /// soft-failure spectrum from the <see cref="SessionSummary"/>. F6 toggles it on
    /// demand (F1 DebugOverlay, F2 Economy, F3 Buildings, F4 Nightmare, F5
    /// MorningReport are taken). Draw only; it never tunes a value.
    /// </summary>
    [DisallowMultipleComponent]
    public class GameOutcomePanel : MonoBehaviour
    {
        [Tooltip("Key that shows/hides the end screen on demand once a verdict exists.")]
        public KeyCode toggleKey = KeyCode.F6;

        [Tooltip("Automatically reveal the panel the moment the outcome is decided.")]
        public bool autoShowOnDecide = true;

        bool _visible;
        bool _haveOutcome;
        SessionSummary _summary;

        Texture2D _vignette;
        GUIStyle _titleStyle;
        GUIStyle _verdictStyle;
        GUIStyle _bodyStyle;

        void OnEnable()
        {
            GameSession.OutcomeDecided += OnOutcomeDecided;
        }

        void OnDisable()
        {
            GameSession.OutcomeDecided -= OnOutcomeDecided;
        }

        void OnOutcomeDecided(SessionSummary summary)
        {
            _summary = summary;
            _haveOutcome = true;
            if (autoShowOnDecide)
            {
                _visible = true;
            }
        }

        void Update()
        {
            if (Input.GetKeyDown(toggleKey) && _haveOutcome)
            {
                _visible = !_visible;
            }
        }

        void OnGUI()
        {
            if (!_visible)
            {
                if (_haveOutcome)
                {
                    GUI.Label(new Rect(8f, Screen.height - 52f, 320f, 22f),
                        $"[{toggleKey}] end screen");
                }
                return;
            }

            EnsureStyles();

            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _vignette);

            float width = Mathf.Min(720f, Screen.width - 80f);
            float height = Mathf.Min(520f, Screen.height - 80f);
            float x = (Screen.width - width) * 0.5f;
            float y = (Screen.height - height) * 0.5f;

            GUILayout.BeginArea(new Rect(x, y, width, height), GUI.skin.box);

            GUILayout.Label("— The First White Night —", _titleStyle);
            GUILayout.Space(10f);

            GUILayout.Label(Headline(_summary), _verdictStyle);
            GUILayout.Space(14f);

            GUILayout.Label(SpectrumBlock(_summary), _bodyStyle);

            GUILayout.FlexibleSpace();
            GUILayout.Label($"[{toggleKey}] close", _bodyStyle);
            GUILayout.EndArea();
        }

        static string Headline(SessionSummary s)
        {
            if (s.Outcome == GameOutcome.Win)
            {
                return "The settlement survived its first White Night.";
            }
            if (s.Outcome == GameOutcome.SurvivedBittersweet)
            {
                return "The settlement survived its first White Night — " +
                       "but few remain to see the spring.";
            }
            switch (s.Reason)
            {
                case LossReason.BellkeeperDead:
                    return "The Bellkeeper fell, and with them the bell fell silent. The settlement is lost.";
                case LossReason.AbbeyFireOut:
                    return "The abbey fire went out. Darkness took what the light had held. The settlement is lost.";
                case LossReason.VillagersLost:
                    return "No villager answered the dawn. The settlement is lost.";
                default:
                    return "The settlement did not survive the White Night.";
            }
        }

        static string SpectrumBlock(SessionSummary s)
        {
            bool survived = s.Outcome == GameOutcome.Win
                            || s.Outcome == GameOutcome.SurvivedBittersweet;
            string villagers = survived
                ? $"{s.VillagersAlive} villagers saw the dawn"
                : $"{s.VillagersAlive} villagers still stood";
            string mood = s.VillagersTerrified ? "terrified" : "hopeful";

            return
                $"{villagers} — {mood}.\n" +
                $"dead {s.VillagersDead}   missing {s.VillagersMissing}   injured {s.VillagersInjured}\n" +
                $"Bellkeeper: {(s.BellkeeperAlive ? $"alive ({Mathf.RoundToInt(s.BellkeeperHealth)} hp)" : "dead")}   " +
                $"abbey fire: {(s.AbbeyFireLit ? "burning" : "out")}\n" +
                $"hound: {s.HoundDisposition}{(s.HoundVanished ? " (vanished)" : "")} " +
                $"(trust {Dir(s.HoundTrustDirection)}, fear {Dir(s.HoundFearDirection)})\n" +
                Shades(s);
        }

        static string Shades(SessionSummary s)
        {
            var shades = new System.Text.StringBuilder("also: ");
            bool any = false;
            void Add(bool cond, string text)
            {
                if (!cond) return;
                if (any) shades.Append(", ");
                shades.Append(text);
                any = true;
            }
            Add(s.AbbeyDamaged, "the abbey was damaged");
            Add(s.SuppliesLow, "supplies ran low");
            Add(s.AnyInjured, "there were injuries");
            Add(s.MissingVillager, "one villager is still missing");
            if (!any)
            {
                shades.Append("the night left few scars");
            }
            return shades.Append('.').ToString();
        }

        static string Dir(int d) => d > 0 ? "rose" : d < 0 ? "fell" : "steady";

        void EnsureStyles()
        {
            if (_vignette == null)
            {
                _vignette = new Texture2D(1, 1);
                _vignette.SetPixel(0, 0, new Color(0.02f, 0.02f, 0.05f, 0.88f));
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
            if (_verdictStyle == null)
            {
                _verdictStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 18,
                    fontStyle = FontStyle.Bold,
                    wordWrap = true
                };
                _verdictStyle.padding = new RectOffset(12, 12, 8, 8);
            }
            if (_bodyStyle == null)
            {
                _bodyStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13,
                    wordWrap = true
                };
                _bodyStyle.padding = new RectOffset(12, 12, 4, 4);
            }
        }
    }
}
