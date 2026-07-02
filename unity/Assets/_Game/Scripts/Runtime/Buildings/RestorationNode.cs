using System.Collections.Generic;
using Abbey.Core;
using UnityEngine;

namespace Abbey.Buildings
{
    /// <summary>The four fixed abbey restoration nodes (VERTICAL_SLICE_SPEC §5).</summary>
    public enum RestorationNodeKind
    {
        AbbeyGate,
        BellTower,
        CandleShrine,
        InfirmaryCorner
    }

    /// <summary>
    /// A fixed, pre-placed abbey restoration site: a <see cref="ConstructionSite"/>
    /// bound to one of the four abbey nodes, created by <see cref="Place"/> at a
    /// map-layout position instead of through <see cref="BuildingPlacer"/> (the
    /// nodes are ruins that already exist — they skip the placement/affordability
    /// planning gate; materials are still paid haul by haul via
    /// <see cref="ConstructionSite.DeliverResource"/>, so nothing changes in the
    /// delivery economy). The gate and bell tower use their own catalog entries
    /// (abbey_gate_repair, bell_tower_repair); the shrine and infirmary reuse the
    /// free-buildable candle_shrine_t1 / infirmary_corner_t1 entries. Completion
    /// effects are catalog-driven inside <see cref="Building.Construct"/> (sacred
    /// light, infirmary zone, <see cref="AbbeyState"/> flags); this component adds
    /// the fixed identity, an "abbey" restored log record and a static registry so
    /// builders/debug tools can list the abbey work. [ExecuteAlways] so EditMode
    /// tests get OnEnable/OnDisable registration.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ConstructionSite))]
    public class RestorationNode : MonoBehaviour
    {
        [Tooltip("Which abbey node this site restores.")]
        public RestorationNodeKind kind;

        static readonly List<RestorationNode> _active = new List<RestorationNode>();

        /// <summary>Every enabled restoration node (completed nodes deactivate with their site).</summary>
        public static IReadOnlyList<RestorationNode> Active => _active;

        /// <summary>Test isolation.</summary>
        public static void ClearRegistry()
        {
            _active.Clear();
        }

        ConstructionSite _site;

        /// <summary>The construction site this node is built on (same GameObject).</summary>
        public ConstructionSite Site
        {
            get
            {
                if (_site == null)
                {
                    _site = GetComponent<ConstructionSite>();
                }
                return _site;
            }
        }

        /// <summary>Snake_case node id used in "abbey" log records.</summary>
        public string NodeId => Id(kind);

        /// <summary>Snake_case id of a node (log vocabulary, distinct from catalog ids).</summary>
        public static string Id(RestorationNodeKind kind)
        {
            switch (kind)
            {
                case RestorationNodeKind.AbbeyGate: return "abbey_gate_repair";
                case RestorationNodeKind.BellTower: return "bell_tower_repair";
                case RestorationNodeKind.CandleShrine: return "candle_shrine";
                case RestorationNodeKind.InfirmaryCorner: return "infirmary_corner";
                default: return kind.ToString().ToLowerInvariant();
            }
        }

        /// <summary>Catalog entry a node is built from.</summary>
        public static string CatalogId(RestorationNodeKind kind)
        {
            switch (kind)
            {
                case RestorationNodeKind.AbbeyGate: return "abbey_gate_repair";
                case RestorationNodeKind.BellTower: return "bell_tower_repair";
                case RestorationNodeKind.CandleShrine: return "candle_shrine_t1";
                case RestorationNodeKind.InfirmaryCorner: return "infirmary_corner_t1";
                default: return null;
            }
        }

        void OnEnable()
        {
            if (!_active.Contains(this))
            {
                _active.Add(this);
            }
        }

        void OnDisable()
        {
            _active.Remove(this);
        }

        /// <summary>
        /// Pre-places a restoration node at a fixed abbey-hill position: the
        /// underlying construction site (initialized straight from the catalog —
        /// no placement gate, the ruin is already there) plus this node component,
        /// logged as an "abbey" record. Returns null when the catalog lacks the
        /// entry. Materials and work flow through the normal ConstructionSite API,
        /// so the Builder job serves abbey nodes with no special casing.
        /// </summary>
        public static RestorationNode Place(
            RestorationNodeKind kind, Vector3 position, BuildingCatalog catalog = null)
        {
            var effectiveCatalog = catalog != null ? catalog : BuildingPlacer.Catalog;
            var type = effectiveCatalog.Find(CatalogId(kind));
            if (type == null)
            {
                GameEventLog.Append("abbey", $"{Id(kind)} node rejected (no catalog entry)");
                return null;
            }

            var go = new GameObject($"RestorationNode_{Id(kind)}");
            go.transform.position = position;
            var site = go.AddComponent<ConstructionSite>();
            site.Initialize(type);
            var node = go.AddComponent<RestorationNode>();
            node.kind = kind;
            site.Completed += node.OnSiteCompleted;
            GameEventLog.Append("abbey",
                $"{Id(kind)} node placed at ({position.x:F1}, {position.z:F1})");
            return node;
        }

        void OnSiteCompleted(ConstructionSite site, Building building)
        {
            // Completion effects already ran inside Building.Construct (catalog
            // FunctionKind); this is the node-identity record the morning report
            // and moral pressures read.
            GameEventLog.Append("abbey", $"{NodeId} restored");
        }
    }
}
