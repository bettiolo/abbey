using Abbey.Core;
using Abbey.Economy;
using UnityEngine;

namespace Abbey.Buildings
{
    /// <summary>Why a placement was refused (<see cref="BuildingPlacer.CanPlaceAt"/>).</summary>
    public enum PlacementError
    {
        None,
        UnknownBuilding,
        Overlapping,
        Unaffordable
    }

    /// <summary>
    /// Pure placement logic, no UI: validates a footprint against everything
    /// already standing (completed <see cref="Building"/>s and pending
    /// <see cref="ConstructionSite"/>s — rects merely touching edges do NOT
    /// overlap, so snug adjacent building is allowed) plus current affordability
    /// (<see cref="ResourceLedger.CanAfford(System.Collections.Generic.IReadOnlyList{ResourceStack})"/> — a
    /// planning gate only; nothing is spent at placement, materials are paid as
    /// they are delivered to the site). Successful placements append a "build"
    /// record to the event log. Deterministic, no RNG.
    /// </summary>
    public static class BuildingPlacer
    {
        static BuildingCatalog _catalog;

        /// <summary>Catalog override for tests; falls back to BuildingCatalog.LoadOrDefault().</summary>
        public static BuildingCatalog Catalog
        {
            get
            {
                if (_catalog == null)
                {
                    _catalog = BuildingCatalog.LoadOrDefault();
                }
                return _catalog;
            }
            set { _catalog = value; }
        }

        /// <summary>Drops the catalog override (test isolation).</summary>
        public static void Clear()
        {
            _catalog = null;
        }

        public static bool CanPlaceAt(string buildingId, Vector3 position)
        {
            return CanPlaceAt(buildingId, position, out _);
        }

        /// <summary>
        /// True when a construction site for <paramref name="buildingId"/> may be
        /// placed centered at <paramref name="position"/> (XZ plane). Checks, in
        /// order: the id exists in the catalog, the footprint overlaps no placed
        /// building or construction site, and the full cost is currently in the
        /// ledger. Pure — no state change, no logging.
        /// </summary>
        public static bool CanPlaceAt(string buildingId, Vector3 position, out PlacementError error)
        {
            var type = Catalog.Find(buildingId);
            if (type == null)
            {
                error = PlacementError.UnknownBuilding;
                return false;
            }

            var rect = type.FootprintAt(position);

            var sites = ConstructionSite.Active;
            for (int i = 0; i < sites.Count; i++)
            {
                if (sites[i] != null && rect.Overlaps(sites[i].Footprint))
                {
                    error = PlacementError.Overlapping;
                    return false;
                }
            }
            var buildings = Building.Active;
            for (int i = 0; i < buildings.Count; i++)
            {
                if (buildings[i] != null && rect.Overlaps(buildings[i].Footprint))
                {
                    error = PlacementError.Overlapping;
                    return false;
                }
            }

            if (!ResourceLedger.CanAfford(type.cost))
            {
                error = PlacementError.Unaffordable;
                return false;
            }

            error = PlacementError.None;
            return true;
        }

        /// <summary>
        /// Places a construction site centered at <paramref name="position"/> and
        /// logs a "build" record. Consumes NOTHING — resources leave the ledger only
        /// as they are delivered to the returned site. Returns null (and logs the
        /// rejection) when <see cref="CanPlaceAt(string,Vector3,out PlacementError)"/> fails.
        /// </summary>
        public static ConstructionSite PlaceConstructionSite(string buildingId, Vector3 position)
        {
            if (!CanPlaceAt(buildingId, position, out var error))
            {
                GameEventLog.Append("build", $"{buildingId} placement rejected ({error})");
                return null;
            }

            var type = Catalog.Find(buildingId);
            var go = new GameObject($"ConstructionSite_{type.id}");
            go.transform.position = position;
            var site = go.AddComponent<ConstructionSite>();
            site.Initialize(type);
            GameEventLog.Append("build",
                $"{type.id} placed at ({position.x:F1}, {position.z:F1})");
            return site;
        }
    }
}
