using Abbey.Buildings;
using Abbey.Core;
using Abbey.Economy;
using UnityEngine;

namespace Abbey.Debugging
{
    /// <summary>
    /// Debug overlay for the construction system (the AGENTS.md "debug overlays for
    /// every hidden system" rule), usable before the Builder villager role exists.
    /// Toggled with <see cref="toggleKey"/> (F3 — F1 is DebugOverlay, F2 the economy
    /// panel) and drawn on the left edge. Lists the catalog (with per-entry
    /// place-ability at the marker position) and every construction site, and lets a
    /// debug user place sites and hand-pump materials/work into them. Display and
    /// debug input only: every cost, footprint and duration shown comes from
    /// <see cref="BuildingCatalog"/> — nothing here holds a balance value.
    /// </summary>
    [DisallowMultipleComponent]
    public class BuildingDebugPanel : MonoBehaviour
    {
        [Tooltip("Key that shows/hides the panel.")]
        public KeyCode toggleKey = KeyCode.F3;

        [Tooltip("Start with the panel visible.")]
        public bool visible;

        [Tooltip("Sites are placed at this transform's position (e.g. the hero). Null = placementPosition.")]
        public Transform placementMarker;

        [Tooltip("Placement position used when no marker transform is assigned.")]
        public Vector3 placementPosition;

        [Tooltip("Builder seconds injected per press of a site's 'work' button (debug pump, not a balance value).")]
        [Min(0.01f)] public float workPumpSeconds = 1f;

        GUIStyle _labelStyle;
        GUIStyle _headerStyle;
        Vector2 _scroll;

        Vector3 MarkerPosition => placementMarker != null
            ? placementMarker.position
            : placementPosition;

        void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                visible = !visible;
            }
        }

        void OnGUI()
        {
            const float width = 360f;
            const float x = 436f; // right of DebugOverlay (F1, x 8 + width 420); economy panel owns the right edge
            const float y = 8f;
            if (!visible)
            {
                const float labelWidth = 220f;
                float labelX = Mathf.Max(8f, Screen.width - labelWidth - 8f);
                GUI.Label(new Rect(labelX, 56f, labelWidth, 22f), $"[{toggleKey}] building panel");
                return;
            }

            EnsureStyles();

            float height = Mathf.Min(Screen.height - y - 8f, 560f);
            GUILayout.BeginArea(new Rect(x, y, width, height), GUI.skin.box);
            _scroll = GUILayout.BeginScrollView(_scroll);

            DrawCatalog();
            DrawSites();
            DrawBuildings();
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

        static string CostText(BuildingType type)
        {
            if (type.cost == null || type.cost.Count == 0)
            {
                return "free";
            }
            var parts = new string[type.cost.Count];
            for (int i = 0; i < type.cost.Count; i++)
            {
                parts[i] = type.cost[i].ToString();
            }
            return string.Join(", ", parts);
        }

        void DrawCatalog()
        {
            var marker = MarkerPosition;
            Header($"Catalog   [{toggleKey}] hide   marker=({marker.x:F1}, {marker.z:F1})");
            var catalog = BuildingPlacer.Catalog;
            for (int i = 0; i < catalog.buildings.Count; i++)
            {
                var type = catalog.buildings[i];
                if (type == null)
                {
                    continue;
                }
                bool ok = BuildingPlacer.CanPlaceAt(type.id, marker, out var error);

                GUILayout.BeginHorizontal();
                GUI.enabled = ok;
                if (GUILayout.Button("place", GUILayout.Width(52f)))
                {
                    BuildingPlacer.PlaceConstructionSite(type.id, marker);
                }
                GUI.enabled = true;
                Line($"{type.id}  {type.footprint.x}x{type.footprint.y}  " +
                     $"{CostText(type)}  {type.buildWorkSeconds}s" +
                     (ok ? "" : $"  [{error}]"));
                GUILayout.EndHorizontal();
            }
        }

        void DrawSites()
        {
            var sites = ConstructionSite.Active;
            Header($"Construction sites ({sites.Count})");
            for (int i = 0; i < sites.Count; i++)
            {
                var site = sites[i];
                if (site == null || site.Type == null)
                {
                    continue;
                }

                string phase = site.NeedsMaterials ? "materials"
                    : site.NeedsWork ? "work" : "done";
                Line($"{site.Type.id}  [{phase}]  work {site.Progress * 100f:F0}%");

                GUILayout.BeginHorizontal();
                GUILayout.Space(12f);
                if (site.NeedsMaterials)
                {
                    for (int r = 0; r < ResourceTypes.Count; r++)
                    {
                        var res = (ResourceType)r;
                        int need = site.RemainingNeed(res);
                        if (need <= 0)
                        {
                            continue;
                        }
                        GUI.enabled = ResourceLedger.Get(res) > 0;
                        if (GUILayout.Button($"+1 {ResourceTypes.Id(res)} ({need} left)"))
                        {
                            site.DeliverResource(res, 1);
                        }
                        GUI.enabled = true;
                    }
                }
                else if (site.NeedsWork)
                {
                    if (GUILayout.Button($"work +{workPumpSeconds:F0}s"))
                    {
                        site.ApplyWork(workPumpSeconds);
                    }
                }
                GUILayout.EndHorizontal();
            }
        }

        void DrawBuildings()
        {
            var buildings = Building.Active;
            Header($"Completed buildings ({buildings.Count})");
            for (int i = 0; i < buildings.Count; i++)
            {
                var building = buildings[i];
                if (building == null || building.Type == null)
                {
                    continue;
                }
                Line($"{building.Id}  [{building.Kind}]  " +
                     $"({building.transform.position.x:F1}, {building.transform.position.z:F1})");
            }
        }

        void DrawLogTail()
        {
            const int tail = 6;
            Header($"Build log (last {tail})");
            var records = GameEventLog.Records;
            int shown = 0;
            for (int i = records.Count - 1; i >= 0 && shown < tail; i--)
            {
                if (records[i].Type != "build")
                {
                    continue;
                }
                Line(records[i].ToString());
                shown++;
            }
        }
    }
}
