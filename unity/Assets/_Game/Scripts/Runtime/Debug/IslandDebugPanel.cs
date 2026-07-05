using System.Collections.Generic;
using Abbey.Island;
using Abbey.Villagers;
using UnityEngine;

namespace Abbey.Debugging
{
    /// <summary>
    /// Debug overlay + trigger surface for island exploration, arrivals and dilemmas (P3-13;
    /// AGENTS.md "debug overlays for every hidden system"). The F-key row (F1-F12) and the
    /// letters L / M / K are taken, so this panel toggles on the mnemonic key
    /// <see cref="toggleKey"/> (I = Island). It lists every POI + discovered state, the live
    /// expeditions with their phase, the arrival forecast/history + spring departures, and
    /// the pending dilemma with its options.
    ///
    /// While the panel is open: number keys 1-3 resolve the pending dilemma; O launches an
    /// expedition (idle villagers) to the nearest hidden POI; U triggers a storm shipwreck;
    /// Y raises the next dilemma card (cycling the deck). Display + trigger only: nothing
    /// here holds or tunes a balance value.
    /// </summary>
    [DisallowMultipleComponent]
    public class IslandDebugPanel : MonoBehaviour
    {
        [Tooltip("Key that shows/hides the panel.")]
        public KeyCode toggleKey = KeyCode.I;

        [Tooltip("Start with the panel visible.")]
        public bool visible;

        [Tooltip("Draw a Scene-view gizmo at each POI (green discovered, magenta hidden).")]
        public bool drawGizmos = true;

        int _cardCursor;
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

            var dilemmas = DilemmaSystem.Instance;
            if (dilemmas != null && dilemmas.PendingCard != null)
            {
                for (int i = 0; i < 3; i++)
                {
                    if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                    {
                        dilemmas.Choose(i);
                    }
                }
            }
            if (Input.GetKeyDown(KeyCode.O))
            {
                LaunchNearestExpedition();
            }
            if (Input.GetKeyDown(KeyCode.U) && ArrivalSystem.Instance != null)
            {
                ArrivalSystem.Instance.TriggerShipwreck();
            }
            if (Input.GetKeyDown(KeyCode.Y))
            {
                RaiseNextCard();
            }
        }

        void LaunchNearestExpedition()
        {
            var exploration = ExplorationSystem.Instance;
            if (exploration == null)
            {
                return;
            }
            var origin = transform.position;
            var target = exploration.NearestHiddenPoi(origin);
            if (target == null)
            {
                return;
            }
            var party = new List<VillagerAgent>();
            var villagers = DuskRecallSystem.Villagers;
            for (int i = 0; i < villagers.Count && party.Count < 3; i++)
            {
                var v = villagers[i];
                if (v != null && v.State == VillagerState.Idle && !exploration.IsAway(v))
                {
                    party.Add(v);
                }
            }
            if (party.Count > 0)
            {
                exploration.LaunchExpedition(target, party);
            }
        }

        void RaiseNextCard()
        {
            var dilemmas = DilemmaSystem.Instance;
            if (dilemmas == null)
            {
                return;
            }
            var deck = dilemmas.Config.dilemmas;
            if (deck == null || deck.Count == 0)
            {
                return;
            }
            var card = deck[_cardCursor % deck.Count];
            _cardCursor++;
            dilemmas.EnqueueCard(card.id);
        }

        void OnGUI()
        {
            float width = 340f;
            float x = 8f;
            float y = Screen.height - 8f - (visible ? 320f : 22f);
            if (!visible)
            {
                GUI.Label(new Rect(x, y, width, 22f), $"[{toggleKey}] island panel");
                return;
            }

            EnsureStyles();
            GUILayout.BeginArea(new Rect(x, y, width, 320f), GUI.skin.box);
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
                _headerStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
            }
        }

        void Header(string text)
        {
            GUILayout.Space(3f);
            GUILayout.Label(text, _headerStyle);
        }

        void Line(string text)
        {
            GUILayout.Label(text, _labelStyle);
        }

        void Draw()
        {
            Header($"Island   [{toggleKey}] hide   O expedition  U shipwreck  Y dilemma");

            var exploration = ExplorationSystem.Instance;
            if (exploration == null)
            {
                Line("no ExplorationSystem in scene");
            }
            else
            {
                var pois = exploration.Pois;
                Line($"POIs {exploration.CountDiscovered()}/{pois.Count} discovered");
                for (int i = 0; i < pois.Count && i < 8; i++)
                {
                    var p = pois[i];
                    Line($"  {(p.discovered ? "[x]" : "[ ]")} {p.type}");
                }
                var exps = exploration.Expeditions;
                Line($"expeditions {exps.Count}  away {exploration.AwayCount}");
                for (int i = 0; i < exps.Count && i < 4; i++)
                {
                    Line($"  {exps[i].Target.type} {exps[i].Phase} party {exps[i].Party.Count}");
                }
            }

            var arrivals = ArrivalSystem.Instance;
            if (arrivals != null)
            {
                Header("Arrivals");
                Line($"trust tier {arrivals.CurrentTrustTier}");
                Line($"stayed {arrivals.StayedCount}  volunteers {arrivals.VolunteeredCount}  " +
                     $"leaving spring {arrivals.LeftCount}");
            }

            var dilemmas = DilemmaSystem.Instance;
            if (dilemmas != null)
            {
                Header($"Dilemma ({dilemmas.PendingCount} queued)");
                var card = dilemmas.PendingCard;
                if (card == null)
                {
                    Line("none pending");
                }
                else
                {
                    Line(card.id);
                    if (card.options != null)
                    {
                        for (int i = 0; i < card.options.Count; i++)
                        {
                            Line($"  {i + 1}. {card.options[i].id}");
                        }
                    }
                }
            }
        }

        void OnDrawGizmos()
        {
            if (!drawGizmos || ExplorationSystem.Instance == null)
            {
                return;
            }
            var pois = ExplorationSystem.Instance.Pois;
            for (int i = 0; i < pois.Count; i++)
            {
                var p = pois[i];
                Gizmos.color = p.discovered ? Color.green : Color.magenta;
                Gizmos.DrawWireSphere(p.position + Vector3.up * 0.5f, 1.2f);
            }
        }
    }
}
