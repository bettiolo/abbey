using System;
using UnityEngine;

namespace Abbey.Island
{
    /// <summary>
    /// The kinds of thing an expedition can turn up in the fog (ROADMAP Phase 3 item 9):
    /// wreckage, old roads, shrines, wells, boundary stones, survivor camps and resource
    /// caches. The reward each yields (resources / seed slots / people / a threat-source
    /// map addition) is data — see <see cref="IslandConfig.RewardFor"/>. New values append
    /// at the end (serialized indices must stay stable).
    /// </summary>
    public enum PoiType
    {
        Wreckage,
        OldRoad,
        Shrine,
        Well,
        BoundaryStone,
        SurvivorCamp,
        ResourceCache
    }

    /// <summary>
    /// One authored point of interest on the island (P3-13). A plain serializable record
    /// held in the <see cref="ExplorationSystem"/> registry — not a MonoBehaviour, so
    /// tests build an island without scene objects. Authored hidden; an expedition that
    /// reaches it flips <see cref="Discovered"/> and the system applies its reward.
    /// </summary>
    [Serializable]
    public class PointOfInterest
    {
        [Tooltip("What sort of place this is (drives the discovery reward table).")]
        public PoiType type = PoiType.ResourceCache;

        [Tooltip("World-space ground position (XZ plane) out in the fog.")]
        public Vector3 position;

        [Tooltip("True once an expedition has reached and revealed it.")]
        public bool discovered;

        public PointOfInterest()
        {
        }

        public PointOfInterest(PoiType type, Vector3 position)
        {
            this.type = type;
            this.position = position;
        }

        /// <summary>True while still hidden in the fog (an expedition target).</summary>
        public bool IsHidden => !discovered;

        public override string ToString()
        {
            return $"{type}@({position.x:F0},{position.z:F0}){(discovered ? " discovered" : " hidden")}";
        }
    }
}
