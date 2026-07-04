using System.Collections.Generic;
using Abbey.Core;
using Abbey.Villagers;
using UnityEngine;

namespace Abbey.Buildings
{
    /// <summary>
    /// The healing aura of a completed infirmary corner (GAME_DESIGN.md §8:
    /// medicine+wood → heal villagers). Villagers are found through the
    /// <see cref="DuskRecallSystem"/> registry (the villager lookup). An Injured
    /// or Resting villager that stays within <see cref="radius"/> for
    /// <see cref="healSeconds"/> continuous seconds is treated: it is put back on
    /// its feet through the public <see cref="VillagerAgent.ForceState"/> hook
    /// (VillagerAgent exposes no partial-heal API — treatment skips the remaining
    /// crawl-to-light/rest time entirely, which is exactly the infirmary's value:
    /// recovery↑). Leaving the radius or changing state resets the exposure timer.
    /// Each heal appends an "abbey" infirmary_heal record. Both tunables come
    /// from <see cref="PrototypeConfig"/> via <see cref="Building.Construct"/> —
    /// no balance lives here. [ExecuteAlways] with manual <see cref="Tick"/> for
    /// EditMode tests, mirroring the other simulation components.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class InfirmaryZone : MonoBehaviour
    {
        [Tooltip("Villagers within this radius of the infirmary get treated (set from PrototypeConfig.infirmaryRadius).")]
        [Min(0f)] public float radius = 4f;

        [Tooltip("Continuous seconds of treatment before an Injured/Resting villager recovers (PrototypeConfig.infirmaryHealSeconds).")]
        [Min(0.01f)] public float healSeconds = 3f;

        [Tooltip("Advance from Update using Time.deltaTime. Tests set false and call Tick().")]
        public bool autoTick = true;

        readonly Dictionary<VillagerAgent, float> _exposure =
            new Dictionary<VillagerAgent, float>();

        void OnDisable()
        {
            _exposure.Clear();
        }

        void Update()
        {
            if (!Application.isPlaying || !autoTick)
            {
                return;
            }
            Tick(Time.deltaTime);
        }

        /// <summary>Deterministic simulation step (autoTick = false in tests).</summary>
        public void Tick(float dt)
        {
            if (dt <= 0f)
            {
                return;
            }

            var villagers = DuskRecallSystem.Villagers;
            for (int i = 0; i < villagers.Count; i++)
            {
                var villager = villagers[i];
                if (villager == null)
                {
                    continue;
                }

                bool treatable = villager.State == VillagerState.Injured
                                 || villager.State == VillagerState.Resting;
                bool inside = PlanarMotion.Distance(
                    villager.transform.position, transform.position) <= radius;
                if (!treatable || !inside)
                {
                    _exposure.Remove(villager); // interrupted treatment starts over
                    continue;
                }

                _exposure.TryGetValue(villager, out float seconds);
                seconds += dt;
                if (seconds < healSeconds)
                {
                    _exposure[villager] = seconds;
                    continue;
                }

                _exposure.Remove(villager);
                villager.ForceState(VillagerState.Idle);
                GameEventLog.Append("abbey", $"infirmary_heal {villager.name}");
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 1f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
