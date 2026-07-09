using Abbey.Economy;
using Abbey.Island;
using Abbey.Map2;
using Abbey.Nightmares;
using UnityEngine;

namespace Abbey.Debugging
{
    /// <summary>
    /// Debug visibility for every hidden Map-2 system. P toggles; while open 1-4
    /// perform indirect Stag interactions, 5 raises the next forest dilemma, and
    /// 6/7 stamp extraction/restoration signals for focused playtesting.
    /// </summary>
    public class Map2DebugPanel : MonoBehaviour
    {
        public bool visible;
        int _nextDilemma;
        Vector2 _scroll;

        static readonly string[] DilemmaIds =
        {
            "old_tree", "starving_deer", "lost_woodcutters", "charcoal_camp",
        };

        void Update()
        {
            if (!Application.isPlaying) return;
            if (Input.GetKeyDown(KeyCode.P)) visible = !visible;
            if (!visible) return;

            var stag = StagCovenantSystem.Instance;
            if (Input.GetKeyDown(KeyCode.Alpha1)) stag?.TryInteract("observe");
            if (Input.GetKeyDown(KeyCode.Alpha2)) stag?.TryInteract("leave_offering");
            if (Input.GetKeyDown(KeyCode.Alpha3)) stag?.TryInteract("tend_wound");
            if (Input.GetKeyDown(KeyCode.Alpha4)) stag?.TryInteract("follow_sign");
            if (Input.GetKeyDown(KeyCode.Alpha5))
            {
                DilemmaSystem.Instance?.EnqueueCard(DilemmaIds[_nextDilemma % DilemmaIds.Length]);
                _nextDilemma++;
            }
            if (Input.GetKeyDown(KeyCode.Alpha6)) stag?.RecordWorldChoice("old_growth_cutting");
            if (Input.GetKeyDown(KeyCode.Alpha7)) stag?.RecordWorldChoice("replanting");
        }

        void OnGUI()
        {
            if (!visible) return;
            GUILayout.BeginArea(new Rect(Screen.width - 430f, 12f, 418f, Screen.height - 24f),
                GUI.skin.box);
            _scroll = GUILayout.BeginScrollView(_scroll);
            GUILayout.Label("ABBEY OF ANTLERS  [P]");

            var carry = CampaignCarryoverSystem.Instance;
            GUILayout.Label($"Carryover trait: {(carry != null ? carry.Trait.ToString() : "None")}");

            var stag = StagCovenantSystem.Instance;
            if (stag != null)
            {
                GUILayout.Label($"Stag: {stag.State}  encounters {stag.Encounters}");
                Bar("Trust", stag.Trust);
                Bar("Patience", stag.Patience);
                Bar("Wound", stag.Wound);
                Bar("Wildness", stag.Wildness);
                Bar("Covenant", stag.Covenant);
            }

            var threat = ThreatSourceSystem.Instance;
            GUILayout.Label($"Forest Debt: {(threat != null ? threat.PressureFor(ThreatSourceType.Forest) : 0f):F2}");

            var scenario = Map2Scenario.Instance;
            if (scenario != null)
            {
                GUILayout.Label($"Nights: {scenario.NightsSurvived}/{scenario.Config.minimumNightsSurvived}");
                GUILayout.Label($"Covenant stock: {scenario.CovenantStockReady}");
                GUILayout.Label($"Exploitative stock: {scenario.ExploitativeStockReady}");
                GUILayout.Label($"Outcome: {scenario.Result} {scenario.LossReason}");
                if (!string.IsNullOrEmpty(scenario.Chronicle)) GUILayout.Label(scenario.Chronicle);
            }

            var dilemma = DilemmaSystem.Instance != null ? DilemmaSystem.Instance.PendingCard : null;
            GUILayout.Label(dilemma != null
                ? $"Dilemma: {dilemma.id} ({dilemma.options.Count} choices; use Island panel I)"
                : "Dilemma: none pending");

            GUILayout.Space(8f);
            GUILayout.Label("1 Observe  2 Offer  3 Tend wound  4 Follow sign");
            GUILayout.Label("5 Raise dilemma  6 Old-growth cut  7 Replant");
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        static void Bar(string label, float value)
        {
            GUILayout.Label($"{label,-10} {value,5:F2}  {new string('█', Mathf.RoundToInt(value * 12f))}");
        }
    }
}
