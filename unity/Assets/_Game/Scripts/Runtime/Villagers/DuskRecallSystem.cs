using System.Collections.Generic;
using Abbey.Core;
using Abbey.Light;
using UnityEngine;

namespace Abbey.Villagers
{
    /// <summary>
    /// The dusk drama beat. Static registry of every <see cref="VillagerAgent"/>
    /// (they register in OnEnable). On PhaseChanged(Dusk) it evaluates each
    /// villager's distance to the nearest Safe point: villagers covered by a recent
    /// BellRang pulse recall immediately with the config speed bonus; villagers
    /// beyond the bell recall late — the one-villager-too-far moment. A bell rung
    /// during Dusk/Night immediately (re)recalls whoever it covers. The registry
    /// doubles as the villager lookup for monsters and the night summary.
    /// </summary>
    public static class DuskRecallSystem
    {
        readonly struct BellPulse
        {
            public readonly Vector3 Position;
            public readonly float Radius;
            public readonly float Time;

            public BellPulse(Vector3 position, float radius, float time)
            {
                Position = position;
                Radius = radius;
                Time = time;
            }
        }

        static readonly List<VillagerAgent> _villagers = new List<VillagerAgent>();
        static readonly List<BellPulse> _pulses = new List<BellPulse>();
        static PrototypeConfig _config;

        /// <summary>Config override for tests; falls back to PrototypeConfig.LoadOrDefault().</summary>
        public static PrototypeConfig Config
        {
            get
            {
                if (_config == null)
                {
                    _config = PrototypeConfig.LoadOrDefault();
                }
                return _config;
            }
            set { _config = value; }
        }

        public static IReadOnlyList<VillagerAgent> Villagers => _villagers;

        public static void Register(VillagerAgent villager)
        {
            if (villager != null && !_villagers.Contains(villager))
            {
                _villagers.Add(villager);
            }
            // Idempotent re-hook: EventBus.ResetAll() in test SetUp wipes static
            // subscriptions, so every Register restores them (unsubscribe first so
            // repeated calls never double-subscribe).
            EventBus.PhaseChanged -= OnPhaseChanged;
            EventBus.PhaseChanged += OnPhaseChanged;
            EventBus.BellRang -= OnBellRang;
            EventBus.BellRang += OnBellRang;
        }

        public static void Unregister(VillagerAgent villager)
        {
            _villagers.Remove(villager);
        }

        /// <summary>Drops villagers, bell pulses, config override and event hooks (test isolation).</summary>
        public static void Clear()
        {
            _villagers.Clear();
            _pulses.Clear();
            _config = null;
            EventBus.PhaseChanged -= OnPhaseChanged;
            EventBus.BellRang -= OnBellRang;
        }

        static float Now => GameClock.Instance != null ? GameClock.Instance.TotalTime : 0f;

        static void OnBellRang(Vector3 position, float radius)
        {
            _pulses.Add(new BellPulse(position, radius, Now));

            // A bell during Dusk/Night immediately recalls (or calms) covered villagers.
            var phase = GameClock.Instance != null ? GameClock.Instance.Phase : DayPhase.Day;
            if (phase != DayPhase.Dusk && phase != DayPhase.Night)
            {
                return;
            }
            for (int i = 0; i < _villagers.Count; i++)
            {
                var v = _villagers[i];
                if (v == null)
                {
                    continue;
                }
                if (PlanarMotion.Distance(v.transform.position, position) <= radius)
                {
                    v.OrderReturnToLight(bellBoosted: true);
                }
            }
        }

        static void OnPhaseChanged(DayPhase phase)
        {
            if (phase == DayPhase.Dusk)
            {
                EvaluateDusk();
            }
        }

        /// <summary>The dusk distance check. Public so tests/debug tools can force it.</summary>
        public static void EvaluateDusk()
        {
            var cfg = Config;
            ExpirePulses(cfg);

            for (int i = 0; i < _villagers.Count; i++)
            {
                var v = _villagers[i];
                if (v == null || v.State == VillagerState.Missing || v.State == VillagerState.Dead)
                {
                    continue;
                }

                Vector3 pos = v.transform.position;
                bool covered = IsCoveredByBell(pos);
                float distToSafe = DarknessEvaluator.Classify(pos) == LightZone.Safe
                    ? 0f
                    : PlanarMotion.Distance(pos, DarknessEvaluator.NearestSafePoint(pos));

                if (!covered && distToSafe > cfg.duskRecallEndangeredDistance)
                {
                    EventBus.RaiseVillagerEndangered(v.gameObject);
                }

                v.OrderReturnToLight(
                    bellBoosted: covered,
                    delaySeconds: covered ? 0f : cfg.duskLateRecallDelaySeconds);
            }
        }

        static bool IsCoveredByBell(Vector3 position)
        {
            for (int i = 0; i < _pulses.Count; i++)
            {
                if (PlanarMotion.Distance(position, _pulses[i].Position) <= _pulses[i].Radius)
                {
                    return true;
                }
            }
            return false;
        }

        static void ExpirePulses(PrototypeConfig cfg)
        {
            float now = Now;
            for (int i = _pulses.Count - 1; i >= 0; i--)
            {
                if (now - _pulses[i].Time > cfg.bellPulseMemorySeconds)
                {
                    _pulses.RemoveAt(i);
                }
            }
        }
    }
}
