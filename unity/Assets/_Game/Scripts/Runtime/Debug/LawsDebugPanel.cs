using Abbey.Core;
using Abbey.Decrees;
using Abbey.Economy;
using Abbey.UI;
using UnityEngine;

namespace Abbey.Debugging
{
    /// <summary>
    /// Debug overlay + decree surface for the standing laws (P3-09; AGENTS.md "debug
    /// overlays for every hidden system"). Toggled with <see cref="toggleKey"/> — the
    /// function-key row F1-F12 is fully allocated (F1 overlay … F12 overdrive), so the laws
    /// panel takes the free mnemonic key <c>L</c>. Lists each group's active option, its
    /// decree cooldown, today's ration math and the durable tags + P3-10 pressures the laws
    /// have accrued. While the panel is open, <b>Shift+1..5</b> cycles the matching group to
    /// its next option (a decree — refused, and logged, while that group is on cooldown), so
    /// the number keys never clash with the overdrive panel's plain 1-7 lever fire.
    /// Display + trigger only: nothing here holds or tunes a balance value.
    /// </summary>
    [DisallowMultipleComponent]
    public class LawsDebugPanel : MonoBehaviour
    {
        [Tooltip("Key that shows/hides the panel (L: the F-key row is taken).")]
        public KeyCode toggleKey = KeyCode.L;

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
            if (!visible)
            {
                return;
            }
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (!shift)
            {
                return;
            }
            var laws = LawSystem.Instance;
            if (laws == null)
            {
                return;
            }
            if (Input.GetKeyDown(KeyCode.Alpha1)) CycleFood(laws);
            if (Input.GetKeyDown(KeyCode.Alpha2)) CycleNightLabour(laws);
            if (Input.GetKeyDown(KeyCode.Alpha3)) CycleBurial(laws);
            if (Input.GetKeyDown(KeyCode.Alpha4)) CycleHound(laws);
            if (Input.GetKeyDown(KeyCode.Alpha5)) CycleOldRites(laws);
        }

        static void CycleFood(LawSystem laws)
        {
            laws.DecreeFood((FoodLaw)(((int)laws.ActiveFood + 1) % 4));
        }

        static void CycleNightLabour(LawSystem laws)
        {
            laws.DecreeNightLabour((NightLabourLaw)(((int)laws.ActiveNightLabour + 1) % 3));
        }

        static void CycleBurial(LawSystem laws)
        {
            laws.DecreeBurial((BurialLaw)(((int)laws.ActiveBurial + 1) % 3));
        }

        static void CycleHound(LawSystem laws)
        {
            laws.DecreeHound((HoundLaw)(((int)laws.ActiveHound + 1) % 4));
        }

        static void CycleOldRites(LawSystem laws)
        {
            laws.DecreeOldRites((OldRitesLaw)(((int)laws.ActiveOldRites + 1) % 2));
        }

        void OnGUI()
        {
            float width = 320f;
            float x = Screen.width - width - 8f - 348f; // left of the overdrive stack
            if (x < 8f)
            {
                x = 8f;
            }
            float y = 8f;
            if (!visible)
            {
                HudHintDock.Draw(HudHintSlot.Laws, $"[{toggleKey}] laws panel");
                return;
            }

            EnsureStyles();
            GUILayout.BeginArea(new Rect(x, y, width, Mathf.Min(Screen.height - y - 8f, 460f)),
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
            var laws = LawSystem.Instance;
            Header($"Laws   [{toggleKey}] hide");
            if (laws == null)
            {
                Line("no LawSystem in scene");
                return;
            }

            var cfg = laws.Config;
            Header("Active laws  (Shift+1..5 to decree)");
            Line($"1. Food        {laws.ActiveFood}  [{LawTags.For(laws.ActiveFood)}]");
            Line($"2. NightLabour {laws.ActiveNightLabour}  [{LawTags.For(laws.ActiveNightLabour)}]");
            Line($"3. Burial      {laws.ActiveBurial}  [{LawTags.For(laws.ActiveBurial)}]");
            Line($"4. Hound       {laws.ActiveHound}  [{LawTags.For(laws.ActiveHound)}]");
            Line($"5. OldRites    {laws.ActiveOldRites}  [{LawTags.For(laws.ActiveOldRites)}]");
            Line($"decree cooldown {cfg.decreeCooldownDays}d");

            var food = cfg.FoodEffectFor(laws.ActiveFood);
            Header("Today's ration");
            Line($"worker {food.workerRation}  idle {food.idleRation}  " +
                 $"hound {food.houndRation}{(food.feedHoundFirst ? " (first)" : "")}");
            Line($"food stock {ResourceLedger.Get(ResourceType.Food)}  " +
                 $"issued last pass {laws.FoodIssuedLastPass}");
            if (food.sanityCostPerVillager > 0f)
            {
                Line($"fasting: -{food.sanityCostPerVillager:F2} sanity, " +
                     $"+{food.hungerPerVillager:F2} hunger each");
            }

            var night = cfg.NightLabourEffectFor(laws.ActiveNightLabour);
            Header("Night labour gate");
            Line($"night work {(night.nightWorkPermitted ? "permitted" : "forbidden")}  " +
                 $"volunteers {laws.NightWorkVolunteersToday}");

            Header("Pressures (P3-10)");
            Line($"hunger {laws.Hunger:F2}  mercy {laws.Mercy:F2}  sanctity {laws.Sanctity:F2}");
            Line($"old-faith {laws.OldFaithPressure:F2}  fear {laws.Fear:F2}  " +
                 $"nightmare {laws.NightmarePressure:F2}");
        }
    }
}
