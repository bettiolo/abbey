using Abbey.Settlement;
using UnityEngine;

namespace Abbey.Debugging
{
    /// <summary>
    /// Debug overlay for the seed-slot settlement system (AGENTS.md "debug overlays
    /// for every hidden system"). Toggled with <see cref="toggleKey"/> (F8, so it
    /// coexists with F1-F7). Shows slot counts by state, the live light debt, and a
    /// short slot list; draws a ground gizmo for every slot (green = Open, amber =
    /// Occupied, grey = Locked) so growth and the hug rule are visible in the Scene
    /// view. Display-only: nothing here holds or tunes a balance value.
    /// </summary>
    [DisallowMultipleComponent]
    public class SettlementDebugPanel : MonoBehaviour
    {
        [Tooltip("Key that shows/hides the panel.")]
        public KeyCode toggleKey = KeyCode.F8;

        [Tooltip("Start with the panel visible.")]
        public bool visible = true;

        [Tooltip("Draw slot ground gizmos in the Scene view even when the panel is hidden.")]
        public bool drawGizmos = true;

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
            float width = 320f;
            float x = Screen.width - width - 8f;
            float y = 804f; // below the season panel's stack on the right edge
            if (!visible)
            {
                GUI.Label(new Rect(x, y, width, 22f), $"[{toggleKey}] settlement panel");
                return;
            }

            EnsureStyles();

            GUILayout.BeginArea(new Rect(x, y, width, Mathf.Min(Screen.height - y - 8f, 200f)),
                GUI.skin.box);

            DrawSlots();

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

        void DrawSlots()
        {
            var system = SeedSlotSystem.Instance;
            Header($"Settlement   [{toggleKey}] hide");
            if (system == null)
            {
                Line("no SeedSlotSystem in scene");
                return;
            }

            int open = system.CountByState(SlotState.Open);
            int occupied = system.CountByState(SlotState.Occupied);
            int locked = system.CountByState(SlotState.Locked);
            Line($"slots {system.Slots.Count}  open {open}  occupied {occupied}  locked {locked}");
            Line($"light debt {system.ComputeLightDebt():F1}");

            var slots = system.Slots;
            int shown = 0;
            for (int i = 0; i < slots.Count && shown < 6; i++)
            {
                var slot = slots[i];
                string origin = slot.IsChild ? $"child<-{slot.parentBuildingId}" : "seed";
                Line($"{slot.state} {slot.sizeClass} {origin} " +
                     $"({slot.position.x:F0},{slot.position.z:F0})");
                shown++;
            }
        }

        void OnDrawGizmos()
        {
            if (!drawGizmos)
            {
                return;
            }
            var system = SeedSlotSystem.Instance;
            if (system == null)
            {
                return;
            }
            var cfg = system.Config;
            var slots = system.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                switch (slot.state)
                {
                    case SlotState.Open:
                        Gizmos.color = new Color(0.4f, 1f, 0.5f, 0.9f);
                        break;
                    case SlotState.Occupied:
                        Gizmos.color = new Color(1f, 0.75f, 0.2f, 0.9f);
                        break;
                    default:
                        Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.6f);
                        break;
                }
                var f = cfg.FootprintFor(slot.sizeClass);
                Gizmos.DrawWireCube(slot.position + new Vector3(0f, 0.05f, 0f),
                    new Vector3(f.x, 0.1f, f.y));
            }
        }
    }
}
