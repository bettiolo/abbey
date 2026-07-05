using Abbey.Core;
using Abbey.Economy;
using UnityEngine;

namespace Abbey.Debugging
{
    /// <summary>
    /// Debug overlay for the salvage economy (the AGENTS.md "debug overlays for
    /// every hidden system" rule). Toggled with <see cref="toggleKey"/> (F2, so it
    /// coexists with DebugOverlay on F1) and drawn on the right edge. Shows the
    /// ledger's stock/capacity, registered storage piles, the recent resource log
    /// tail and every salvage site's stage + remaining pool. Display-only: nothing
    /// here holds or tunes a balance value.
    /// </summary>
    [DisallowMultipleComponent]
    public class EconomyDebugPanel : MonoBehaviour
    {
        [Tooltip("Key that shows/hides the panel.")]
        public KeyCode toggleKey = KeyCode.F2;

        [Tooltip("Start with the panel visible.")]
        public bool visible;

        [Tooltip("How many trailing 'resource' log records to show.")]
        [Min(0)] public int logTailCount = 6;

        GUIStyle _labelStyle;
        GUIStyle _headerStyle;
        Vector2 _scroll;

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
            float x = Screen.width - width - 8f;
            if (!visible)
            {
                GUI.Label(new Rect(8f, 96f, 220f, 22f), $"[{toggleKey}] economy panel");
                return;
            }

            EnsureStyles();

            float height = Mathf.Min(Screen.height - 16f, 560f);
            GUILayout.BeginArea(new Rect(x, 8f, width, height), GUI.skin.box);
            _scroll = GUILayout.BeginScrollView(_scroll);

            DrawLedger();
            DrawProductionBuildings();
            DrawSalvageSites();
            DrawLogTail();

            GUILayout.EndScrollView();
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

        void DrawLedger()
        {
            Header($"Stockpile   [{toggleKey}] hide");
            Line($"stored={ResourceLedger.TotalStored}/{ResourceLedger.Capacity}  " +
                 $"piles={ResourceLedger.StoragePiles.Count}");
            for (int i = 0; i < ResourceTypes.Count; i++)
            {
                var type = (ResourceType)i;
                int amount = ResourceLedger.Get(type);
                if (amount > 0)
                {
                    Line($"{ResourceTypes.Id(type)} = {amount}");
                }
            }
        }

        void DrawProductionBuildings()
        {
            var buildings = ProductionBuilding.Active;
            Header($"Production ({buildings.Count})");
            var season = Abbey.World.SeasonSystem.Instance != null
                ? Abbey.World.SeasonSystem.Instance.CurrentSeason.ToString()
                : "—";
            Line($"season={season}");
            for (int i = 0; i < buildings.Count; i++)
            {
                var b = buildings[i];
                if (b == null)
                {
                    continue;
                }
                var recipe = b.Recipe;
                string kind = recipe == null ? "?" : (recipe.seasonal ? "growth" : "convert");
                Line($"{b.BuildingId} [{kind}] staff={b.StaffedWorkers}" +
                     (b.IsStaffed ? "" : " (idle)"));
                Line($"  cycle {b.CycleProgress01 * 100f:F0}%  done={b.CompletedCycles}");
            }
        }

        void DrawSalvageSites()
        {
            var sites = SalvageSite.Active;
            Header($"Salvage sites ({sites.Count})");
            for (int i = 0; i < sites.Count; i++)
            {
                var site = sites[i];
                if (site == null)
                {
                    continue;
                }
                Line($"{site.name}  {site.Stage}  " +
                     $"{site.RemainingFraction * 100f:F0}% left ({site.TotalRemaining} units)");
                Line($"  wood={site.Remaining(ResourceType.Wood)}  " +
                     $"food={site.Remaining(ResourceType.Food)}  " +
                     $"oil={site.Remaining(ResourceType.Oil)}  " +
                     $"med={site.Remaining(ResourceType.Medicine)}");
            }
        }

        void DrawLogTail()
        {
            Header($"Resource log (last {logTailCount})");
            var records = GameEventLog.Records;
            int shown = 0;
            for (int i = records.Count - 1; i >= 0 && shown < logTailCount; i--)
            {
                if (records[i].Type != "resource" && records[i].Type != "salvage"
                    && records[i].Type != "production")
                {
                    continue;
                }
                Line(records[i].ToString());
                shown++;
            }
        }
    }
}
