using System;
using Abbey.World;
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
        public static event Action<int> DayChanged;
        public static event Action<Vector3, float> BellRang;
        public static event Action<GameObject> VillagerEndangered;
        public static event Action<GameObject> VillagerRescued;
        public static event Action<float> HoundFed;
        public static event Action<GameObject> MonsterSpawned;
        public static event Action<Season> SeasonChanged;
        public static event Action<Weather> WeatherChanged;
        public static event Action<string> OmenAppeared;

        public static void RaisePhaseChanged(DayPhase phase)
        {
            GameEventLog.Append("PhaseChanged", phase.ToString());
            PhaseChanged?.Invoke(phase);
        }

        /// <summary>The day counter advanced (the calendar's callback surface).</summary>
        public static void RaiseDayChanged(int dayNumber)
        {
            GameEventLog.Append("DayChanged", dayNumber.ToString());
            DayChanged?.Invoke(dayNumber);
        }

        public static void RaiseSeasonChanged(Season season)
        {
            GameEventLog.Append("SeasonChanged", season.ToString());
            SeasonChanged?.Invoke(season);
        }

        public static void RaiseWeatherChanged(Weather weather)
        {
            GameEventLog.Append("WeatherChanged", weather.ToString());
            WeatherChanged?.Invoke(weather);
        }

        public static void RaiseOmenAppeared(string description)
        {
            GameEventLog.Append("OmenAppeared", description);
            OmenAppeared?.Invoke(description);
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
            DayChanged = null;
            BellRang = null;
            VillagerEndangered = null;
            VillagerRescued = null;
            HoundFed = null;
            MonsterSpawned = null;
            SeasonChanged = null;
            WeatherChanged = null;
            OmenAppeared = null;
        }
    }
}
