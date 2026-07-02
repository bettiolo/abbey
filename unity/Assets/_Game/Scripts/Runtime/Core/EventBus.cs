using System;
using UnityEngine;

namespace Abbey.Core
{
    /// <summary>
    /// Static event hub with explicit signatures. Systems subscribe in OnEnable and
    /// unsubscribe in OnDisable. Every Raise* helper also appends to
    /// <see cref="GameEventLog"/> so the bus and the log never disagree.
    /// Tests call <see cref="ResetAll"/> in [SetUp] for isolation.
    /// </summary>
    public static class EventBus
    {
        public static event Action<DayPhase> PhaseChanged;
        public static event Action<Vector3, float> BellRang;
        public static event Action<GameObject> VillagerEndangered;
        public static event Action<GameObject> VillagerRescued;
        public static event Action<float> HoundFed;
        public static event Action<GameObject> MonsterSpawned;

        public static void RaisePhaseChanged(DayPhase phase)
        {
            GameEventLog.Append("PhaseChanged", phase.ToString());
            PhaseChanged?.Invoke(phase);
        }

        public static void RaiseBellRang(Vector3 position, float radius)
        {
            GameEventLog.Append("BellRang", $"pos={position} radius={radius:F2}");
            BellRang?.Invoke(position, radius);
        }

        public static void RaiseVillagerEndangered(GameObject villager)
        {
            GameEventLog.Append("VillagerEndangered", villager != null ? villager.name : "<null>");
            VillagerEndangered?.Invoke(villager);
        }

        public static void RaiseVillagerRescued(GameObject villager)
        {
            GameEventLog.Append("VillagerRescued", villager != null ? villager.name : "<null>");
            VillagerRescued?.Invoke(villager);
        }

        public static void RaiseHoundFed(float newTrust)
        {
            GameEventLog.Append("HoundFed", $"trust={newTrust:F3}");
            HoundFed?.Invoke(newTrust);
        }

        public static void RaiseMonsterSpawned(GameObject monster)
        {
            GameEventLog.Append("MonsterSpawned", monster != null ? monster.name : "<null>");
            MonsterSpawned?.Invoke(monster);
        }

        /// <summary>Clears every subscriber. Call from test [SetUp] for isolation.</summary>
        public static void ResetAll()
        {
            PhaseChanged = null;
            BellRang = null;
            VillagerEndangered = null;
            VillagerRescued = null;
            HoundFed = null;
            MonsterSpawned = null;
        }
    }
}
