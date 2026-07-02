using Abbey.Core;
using Abbey.Hero;
using Abbey.Light;
using UnityEngine;

namespace Abbey.Nightmares
{
    /// <summary>
    /// The lantern moth (GAME_DESIGN.md §9): harmless-looking — it never attacks
    /// anyone — but dangerous because it creates darkness gaps. It flies to the
    /// weakest lit, non-sacred, finite-fuel <see cref="LightSource"/> and drains
    /// its fuel fast until the light dies. It flees when the Bellkeeper comes
    /// close, and the bell stuns it (spec: bell = weak-nightmare stun, handled by
    /// the base class via <see cref="IsStunnedByBell"/>). Light does not repel it:
    /// moths fly INTO the light.
    /// </summary>
    [ExecuteAlways]
    public class LanternMothController : MonsterController
    {
        BellkeeperController _hero;
        LightSource _drainTarget;
        bool _wasFleeingHero;

        public override NightmareType Type => NightmareType.LanternMoth;

        protected override bool IsStunnedByBell => true;

        /// <summary>The light it is currently draining toward/at (debug/tests).</summary>
        public LightSource DrainTarget => _drainTarget;

        /// <summary>Lazily found in the scene; tests may inject one.</summary>
        public BellkeeperController Hero
        {
            get
            {
                if (_hero == null)
                {
                    _hero = FindFirstObjectByType<BellkeeperController>();
                }
                return _hero;
            }
            set { _hero = value; }
        }

        protected override void TickBehaviour(PrototypeConfig cfg, float dt)
        {
            // The Bellkeeper scares it off its light.
            var hero = Hero;
            if (hero != null && hero.IsAlive
                && PlanarMotion.Distance(transform.position, hero.transform.position)
                   <= cfg.lanternMothFleeRange)
            {
                if (!_wasFleeingHero)
                {
                    _wasFleeingHero = true;
                    GameEventLog.Append("nightmare", $"moth_flees_bellkeeper name={name}");
                }
                transform.position += PlanarMotion.Direction(
                                          hero.transform.position, transform.position)
                                      * cfg.lanternMothFleeSpeed * dt;
                return;
            }
            _wasFleeingHero = false;

            if (_drainTarget == null || !_drainTarget.isLit)
            {
                _drainTarget = FindWeakestDrainableLight();
                if (_drainTarget == null)
                {
                    return; // nothing left to dim
                }
                GameEventLog.Append("nightmare",
                    $"moth_targets_light name={name} light={_drainTarget.name}");
            }

            float dist = PlanarMotion.Distance(
                transform.position, _drainTarget.transform.position);
            if (dist > cfg.lanternMothDrainRange)
            {
                // Fly straight at the glow — light never repels a moth.
                transform.position = PlanarMotion.Step(
                    transform.position, _drainTarget.transform.position,
                    cfg.lanternMothMoveSpeed, dt, cfg.lanternMothDrainRange * 0.5f, out _);
                return;
            }

            // Clinging: drain fuel fast. The light dies, the gap opens.
            _drainTarget.fuelSeconds -= cfg.lanternMothDrainPerSecond * dt;
            if (_drainTarget.fuelSeconds <= 0f)
            {
                _drainTarget.fuelSeconds = 0f;
                _drainTarget.Extinguish();
                GameEventLog.Append("nightmare",
                    $"moth_drained_light name={name} light={_drainTarget.name}");
                _drainTarget = null;
            }
        }

        /// <summary>Moths fly into light: nothing is ever too bright for one.</summary>
        protected override bool IsTooBright(Vector3 position, PrototypeConfig cfg)
        {
            return false;
        }

        /// <summary>
        /// The weakest drainable light: lit, non-sacred, finite fuel, smallest
        /// effective radius (first registered wins ties — deterministic).
        /// </summary>
        LightSource FindWeakestDrainableLight()
        {
            LightSource weakest = null;
            float bestRadius = float.MaxValue;
            var sources = DarknessEvaluator.Sources;
            for (int i = 0; i < sources.Count; i++)
            {
                var source = sources[i];
                if (source == null || !source.isLit || source.sacred || source.HasInfiniteFuel)
                {
                    continue;
                }
                float effective = source.EffectiveRadius;
                if (effective <= 0f)
                {
                    continue;
                }
                if (effective < bestRadius)
                {
                    bestRadius = effective;
                    weakest = source;
                }
            }
            return weakest;
        }
    }
}
