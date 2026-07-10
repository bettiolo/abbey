using System;
using Abbey.Decrees;
using Abbey.Economy;
using Abbey.UI;
using UnityEngine;

namespace Abbey.Debugging
{
    /// <summary>
    /// Debug overlay + trigger surface for the emergency overdrive levers (P3-08;
    /// AGENTS.md "debug overlays for every hidden system"). Toggled with
    /// <see cref="toggleKey"/> (F12 — the other panels own F1-F11). Lists every lever with
    /// its permitted / affordable state and lets you fire one; shows the levers active
    /// tonight (participants, candle carriers, overburned lanterns), tonight's booked
    /// nightmare debt and the accumulated pending debt, plus the trust / beast-status
    /// pressure stubs. The number keys 1-7 fire the matching lever while the panel is open.
    /// Display + trigger only: nothing here holds or tunes a balance value.
    /// </summary>
    [DisallowMultipleComponent]
    public class OverdriveDebugPanel : MonoBehaviour
    {
        [Tooltip("Key that shows/hides the panel.")]
        public KeyCode toggleKey = KeyCode.F12;

        [Tooltip("Start with the panel visible.")]
        public bool visible;

        static readonly OverdriveActionId[] Actions =
            (OverdriveActionId[])Enum.GetValues(typeof(OverdriveActionId));

        GUIStyle _labelStyle;
        GUIStyle _headerStyle;

        void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                visible = !visible;
            }
            if (!visible)
            {
                return;
            }
            // Number keys 1..7 fire the matching lever (Alpha1 = first action).
            for (int i = 0; i < Actions.Length; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    var system = OverdriveSystem.Instance;
                    if (system != null)
                    {
                        system.Activate(Actions[i]);
                    }
                }
            }
        }

        void OnGUI()
        {
            float width = 340f;
            float x = Screen.width - width - 8f; // right stack, clear of the left panels
            float y = 8f;
            if (!visible)
            {
                HudHintDock.Draw(HudHintSlot.Overdrive, $"[{toggleKey}] overdrive panel");
                return;
            }

            EnsureStyles();
            GUILayout.BeginArea(new Rect(x, y, width, Mathf.Min(Screen.height - y - 8f, 420f)),
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
            var system = OverdriveSystem.Instance;
            Header($"Overdrive   [{toggleKey}] hide");
            if (system == null)
            {
                Line("no OverdriveSystem in scene");
                return;
            }

            var cfg = system.Config;
            Line($"pending debt {system.PendingNightmareDebt:F1}   " +
                 $"tonight +{system.TonightDebtAccrual:F1}");
            Line($"trust {TrustLedgerValue():F2}   beast {BeastLedgerValue():F2}");

            Header("Levers  (1-7 to fire)");
            for (int i = 0; i < Actions.Length; i++)
            {
                var id = Actions[i];
                var def = cfg.DefFor(id);
                bool permitted = system.IsPermitted(id);
                bool affordable = def.immediateCost == null
                                  || ResourceLedger.CanAfford(def.immediateCost);
                string flags = permitted ? (affordable ? "" : " [poor]") : " [barred]";
                Line($"{i + 1}. {id}{flags}");
            }

            Header("Active tonight");
            var active = system.ActiveActions;
            if (active.Count == 0)
            {
                Line("none");
            }
            for (int i = 0; i < active.Count; i++)
            {
                var a = active[i];
                Line($"{a.Id}{(a.Active ? "" : " (stood down)")}  " +
                     $"villagers {a.Participants.Count}  candles {a.CandleLights.Count}  " +
                     $"overburn {a.OverburnedLanterns.Count}");
            }
        }

        static float TrustLedgerValue()
        {
            return Abbey.Core.TrustLedger.Trust;
        }

        static float BeastLedgerValue()
        {
            return Abbey.Core.BeastStatusLedger.BeastStatus;
        }
    }
}
