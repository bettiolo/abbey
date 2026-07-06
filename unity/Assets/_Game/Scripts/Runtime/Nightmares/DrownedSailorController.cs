using Abbey.Core;
using Abbey.Light;
using UnityEngine;

namespace Abbey.Nightmares
{
    /// <summary>
    /// The drowned sailor (GAME_DESIGN.md §9): rises near the wreck only when
    /// someone died by water (the director gates the spawn on the event log).
    /// Slow and dripping, it walks a straight dread-line toward the nearest lit
    /// zone. Extinguish-resistant: it tolerates far more light than a pale hound
    /// (drownedSailorLightTolerance) so ordinary lanterns barely slow it — but it
    /// still refuses Safe ground, retreats from sacred light, and breaks off when
    /// the Black Hound presses it. It strikes any villager it plods within reach
    /// of on the way.
    /// </summary>
    [ExecuteAlways]
    public class DrownedSailorController : MonsterController
    {
        bool _loggedSacredRepel;

        public override NightmareType Type => NightmareType.DrownedSailor;

        protected override void TickBehaviour(PrototypeConfig cfg, float dt)
        {
            // Retreats from the hound (slowly — it is never fast).
            if (TickFleeFromThreat(cfg, cfg.drownedSailorMoveSpeed, dt))
            {
                return;
            }

            // Sacred light repels it outright.
            if (RetreatFromSacredLight(cfg, dt))
            {
                return;
            }

            // Anyone in arm's reach on the dread-line gets struck.
            var victim = FindTargetVillager(cfg);
            if (victim != null
                && PlanarMotion.Distance(transform.position, victim.transform.position)
                   <= cfg.monsterAttackRange)
            {
                TryAttack(victim, cfg);
                return;
            }

            // The dread-line: straight toward the nearest lit source.
            var beacon = NearestLitSource();
            if (beacon == null)
            {
                return; // a world with no light has nothing to dread-line toward
            }
            Vector3 next = PlanarMotion.StepAroundBuildings(
                transform.position, beacon.transform.position,
                cfg.drownedSailorMoveSpeed, dt, cfg.arrivalRadius,
                cfg.movementObstaclePadding, out _);
            TryMoveTo(next, cfg);
        }

        /// <summary>
        /// Extinguish-resistant: only Safe ground or intensity beyond the sailor's
        /// (high) tolerance stops it. It looms deep into the Edge of common lights.
        /// </summary>
        protected override bool IsTooBright(Vector3 position, PrototypeConfig cfg)
        {
            return DarknessEvaluator.Classify(position) == LightZone.Safe
                   || DarknessEvaluator.LightIntensityAt(position) > cfg.drownedSailorLightTolerance;
        }

        /// <summary>
        /// Steps directly away from any lit sacred source whose territory it has
        /// entered. Returns true when a retreat step consumed this tick.
        /// </summary>
        bool RetreatFromSacredLight(PrototypeConfig cfg, float dt)
        {
            LightSource repelling = null;
            float bestDist = float.MaxValue;
            var sources = DarknessEvaluator.Sources;
            for (int i = 0; i < sources.Count; i++)
            {
                var source = sources[i];
                if (source == null || !source.sacred || !source.isLit)
                {
                    continue;
                }
                float effective = source.EffectiveRadius;
                if (effective <= 0f)
                {
                    continue;
                }
                float dist = PlanarMotion.Distance(transform.position, source.transform.position);
                if (dist < effective && dist < bestDist)
                {
                    bestDist = dist;
                    repelling = source;
                }
            }

            if (repelling == null)
            {
                _loggedSacredRepel = false;
                return false;
            }

            if (!_loggedSacredRepel)
            {
                _loggedSacredRepel = true;
                GameEventLog.Append("nightmare",
                    $"sacred_repel name={name} light={repelling.name}");
            }
            transform.position = PlanarMotion.MoveAroundBuildings(
                transform.position,
                PlanarMotion.Direction(repelling.transform.position, transform.position)
                * cfg.drownedSailorMoveSpeed * dt,
                cfg.movementObstaclePadding);
            return true;
        }

        /// <summary>The nearest lit source of any kind — the glow it walks toward.</summary>
        LightSource NearestLitSource()
        {
            LightSource nearest = null;
            float bestDist = float.MaxValue;
            var sources = DarknessEvaluator.Sources;
            for (int i = 0; i < sources.Count; i++)
            {
                var source = sources[i];
                if (source == null || !source.isLit || source.EffectiveRadius <= 0f)
                {
                    continue;
                }
                float dist = PlanarMotion.Distance(transform.position, source.transform.position);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    nearest = source;
                }
            }
            return nearest;
        }
    }
}
