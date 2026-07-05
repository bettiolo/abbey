using UnityEngine;

namespace Abbey.Nightmares
{
    /// <summary>
    /// The shared controller for the five P3-11 consequence nightmares (Hunger Wight, Dead
    /// Worker, Grave Crawler, Chain Hound, Faceless Saint). Their hunt behaviour is exactly
    /// the <see cref="MonsterController"/> base (close on the most-exposed villager, refuse
    /// Safe light, assault a door when no one is exposed, flee the Black Hound) — the
    /// consequences differ in WHEN and WHERE they appear, not in how they move — so a single
    /// controller carries all five, its species type and per-type stats
    /// (health scale, bell-stun) injected from <see cref="ThreatConfig"/>. [ExecuteAlways] so
    /// the static registry works in EditMode tests.
    /// </summary>
    [ExecuteAlways]
    public class ConsequenceMonsterController : MonsterController
    {
        NightmareType _type = NightmareType.HungerWight;
        float _healthScale = 1f;
        bool _stunnedByBell = true;

        public override NightmareType Type => _type;

        protected override float HealthScale => _healthScale;

        protected override bool IsStunnedByBell => _stunnedByBell;

        /// <summary>
        /// Binds the species + its stats before <see cref="MonsterController.Configure"/> runs,
        /// so the health reset picks up the per-type scale.
        /// </summary>
        public void ConfigureConsequence(NightmareType type, ThreatConfig cfg)
        {
            _type = type;
            if (cfg != null)
            {
                var rule = cfg.RuleFor(type);
                _healthScale = Mathf.Max(0.01f, rule.healthScale);
                _stunnedByBell = rule.stunnedByBell;
            }
        }
    }
}
