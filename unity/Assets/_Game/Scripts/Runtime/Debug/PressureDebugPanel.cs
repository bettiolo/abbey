using Abbey.Buildings;
using Abbey.Decrees;
using Abbey.Morale;
using UnityEngine;

namespace Abbey.Debugging
{
    /// <summary>
    /// Debug overlay for the moral pressures + abbey transformation (P3-10; AGENTS.md "debug
    /// overlays for every hidden system"). Toggled with <see cref="toggleKey"/> — the
    /// function-key row F1-F12 is full and L is the laws panel, so the pressures panel takes
    /// the free mnemonic key <c>M</c> (Moral). Lists every pressure with its trust tier, the
    /// two external inputs (beast status, household sanity), the current abbey form + its
    /// modifier line, and each candidate form's score against its activation threshold so the
    /// "why" is legible. Display only: nothing here holds or tunes a balance value.
    /// </summary>
    [DisallowMultipleComponent]
    public class PressureDebugPanel : MonoBehaviour
    {
        [Tooltip("Key that shows/hides the panel (M: the F-key row and L are taken).")]
        public KeyCode toggleKey = KeyCode.M;

        [Tooltip("Start with the panel visible.")]
        public bool visible;

        GUIStyle _labelStyle;
        GUIStyle _headerStyle;

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
            float x = 8f;
            float y = Screen.height - 8f - 300f;
            if (y < 8f)
            {
                y = 8f;
            }
            if (!visible)
            {
                GUI.Label(new Rect(x, y, width, 22f), $"[{toggleKey}] pressures panel");
                return;
            }

            EnsureStyles();
            GUILayout.BeginArea(new Rect(x, y, width, Mathf.Min(Screen.height - y - 8f, 300f)),
                GUI.skin.box);
            Draw();
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

        void Draw()
        {
            var p = PressureSystem.Instance;
            Header($"Pressures   [{toggleKey}] hide");
            if (p == null)
            {
                Line("no PressureSystem in scene");
                return;
            }

            Line($"trust    {p.Trust:F2}  [{p.TrustTier}]");
            Line($"sanctity {p.Sanctity:F2}   mercy  {p.Mercy:F2}");
            Line($"fear     {p.Fear:F2}   reason {p.Reason:F2}");
            Line($"hunger   {p.Hunger:F2}   oldfaith {p.OldFaith:F2}");
            Line($"beast {p.BeastStatus:F2}   household sanity {p.HouseholdSanity:F2}");

            var snap = p.Snapshot();
            var transform = AbbeyTransformationSystem.Instance;
            Header("Abbey form");
            if (transform == null)
            {
                Line($"AbbeyState form: {AbbeyState.CurrentForm}");
            }
            else
            {
                Line($"{transform.CurrentForm}  (score {transform.LastScore:F2})");
            }
            var mods = AbbeyState.Modifiers;
            Line($"modifier: {(string.IsNullOrEmpty(mods.note) ? "none" : mods.note)}");

            Header("Why (form scores vs threshold)");
            var cfg = p.Config;
            if (cfg.formRules != null)
            {
                for (int i = 0; i < cfg.formRules.Count; i++)
                {
                    var rule = cfg.formRules[i];
                    if (rule == null)
                    {
                        continue;
                    }
                    float score = AbbeyTransformationSystem.Score(rule, snap);
                    string mark = score >= rule.activationThreshold ? " *" : "";
                    Line($"{rule.form,-10} {score:F2} / {rule.activationThreshold:F2}{mark}");
                }
            }

            var laws = LawSystem.Instance;
            if (laws != null)
            {
                Header("Active law tags");
                Line(string.Join("  ", laws.ActiveTags()));
            }
        }
    }
}
