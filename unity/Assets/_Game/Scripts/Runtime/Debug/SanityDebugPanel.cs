using System.Text;
using Abbey.Sanity;
using Abbey.UI;
using UnityEngine;

namespace Abbey.Debugging
{
    /// <summary>
    /// Debug overlay for the sanity/dread/asylum system (AGENTS.md "debug overlays
    /// for every hidden system"). Toggled with <see cref="toggleKey"/> (F9, so it
    /// coexists with the other debug panels; player HUD/minimap are F7/F8). Surfaces the hidden per-villager state: a sanity + dread
    /// bar and band per tracked villager, the asylum roster, and tonight's held
    /// ("missing") units. Display-only: nothing here holds or tunes a balance value.
    /// </summary>
    [DisallowMultipleComponent]
    public class SanityDebugPanel : MonoBehaviour
    {
        [Tooltip("Key that shows/hides the panel.")]
        public KeyCode toggleKey = KeyCode.F9;

        [Tooltip("Start with the panel visible.")]
        public bool visible;

        [Tooltip("Max villager rows to list.")]
        [Min(1)] public int maxRows = 10;

        GUIStyle _labelStyle;
        GUIStyle _headerStyle;
        readonly StringBuilder _bar = new StringBuilder(16);

        void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                visible = !visible;
            }
        }

        void OnGUI()
        {
            float width = 320f;
            float x = 8f; // left edge, away from the right-hand season/settlement stack
            float y = 8f;
            if (!visible)
            {
                HudHintDock.Draw(HudHintSlot.Sanity, $"[{toggleKey}] sanity panel");
                return;
            }

            EnsureStyles();

            GUILayout.BeginArea(new Rect(x, y, width, Mathf.Min(Screen.height - y - 8f, 320f)),
                GUI.skin.box);

            DrawSanity();

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

        void DrawSanity()
        {
            var system = SanitySystem.Instance;
            Header($"Sanity   [{toggleKey}] hide");
            if (system == null)
            {
                Line("no SanitySystem in scene");
                return;
            }

            var records = system.Records;
            int held = 0;
            for (int i = 0; i < records.Count; i++)
            {
                if (records[i].HeldInAsylum)
                {
                    held++;
                }
            }
            var asylum = system.Asylum;
            Line($"tracked {records.Count}  avg sanity {system.AverageSanity():F2}  " +
                 $"asylum {(asylum != null ? asylum.AdmittedCount.ToString() : "none")}  held {held}");

            int shown = 0;
            for (int i = 0; i < records.Count && shown < maxRows; i++)
            {
                var r = records[i];
                if (r.Villager == null)
                {
                    continue;
                }
                string flag = r.HeldInAsylum ? " [asylum]" : (r.IsInsane ? " [insane]" : "");
                Line($"{Trim(r.Villager.name)} {r.State}{flag}");
                Line($"  san {Bar(r.Sanity)} {r.Sanity:F2}  dread {Bar(r.Dread)} {r.Dread:F2}");
                shown++;
            }
        }

        string Bar(float value)
        {
            const int slots = 10;
            int filled = Mathf.Clamp(Mathf.RoundToInt(value * slots), 0, slots);
            _bar.Length = 0;
            for (int i = 0; i < slots; i++)
            {
                _bar.Append(i < filled ? '#' : '.');
            }
            return _bar.ToString();
        }

        static string Trim(string name)
        {
            return name.Length <= 16 ? name : name.Substring(0, 16);
        }
    }
}
