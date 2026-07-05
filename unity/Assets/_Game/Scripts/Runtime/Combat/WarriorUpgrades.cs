using System;
using System.Collections.Generic;
using Abbey.Economy;
using UnityEngine;

namespace Abbey.Combat
{
    /// <summary>
    /// One tier in the warrior lodge's data-driven upgrade tree (P3-06). A tier
    /// debits its <see cref="cost"/> from the <see cref="ResourceLedger"/> (Phase 3
    /// tools/scrap_iron/coal economy) and adds its stat deltas — cumulatively — to
    /// every warrior housed at the lodge. All numbers live in the
    /// <see cref="CombatConfig"/> asset that owns the tier list, never in code
    /// (AGENTS.md: no balance inside MonoBehaviours).
    /// </summary>
    [Serializable]
    public class WarriorUpgradeTier
    {
        [Tooltip("Snake_case id for the event log / debug overlay.")]
        public string id = "tier";

        public string displayName = "Upgrade";

        [Tooltip("Materials debited from the ledger to purchase this tier (duplicate types accumulate).")]
        public List<ResourceStack> cost = new List<ResourceStack>();

        [Tooltip("Added to warrior max health when this tier is applied.")]
        public float healthDelta;

        [Tooltip("Added to warrior attack damage when this tier is applied.")]
        public float damageDelta;

        [Tooltip("Added to the warrior attack cooldown (negative = faster strikes) when this tier is applied.")]
        public float attackCooldownDelta;

        [Tooltip("Subtracted from the fraction of Dark-band sanity drain a warrior suffers (positive = tougher in the dark).")]
        public float darkSanityResistDelta;
    }

    /// <summary>
    /// The resolved stats of a warrior at a given upgrade tier. Pure data produced
    /// by <see cref="WarriorUpgrades.StatsAtTier"/>; the <see cref="WarriorAgent"/>
    /// fights from these values.
    /// </summary>
    public readonly struct WarriorStats
    {
        public readonly float MaxHealth;
        public readonly float AttackDamage;
        public readonly float AttackRange;
        public readonly float MoveSpeed;
        public readonly float AttackCooldownSeconds;
        public readonly float DarkSanityDrainFraction;

        public WarriorStats(float maxHealth, float attackDamage, float attackRange,
            float moveSpeed, float attackCooldownSeconds, float darkSanityDrainFraction)
        {
            MaxHealth = maxHealth;
            AttackDamage = attackDamage;
            AttackRange = attackRange;
            MoveSpeed = moveSpeed;
            AttackCooldownSeconds = attackCooldownSeconds;
            DarkSanityDrainFraction = darkSanityDrainFraction;
        }
    }

    /// <summary>
    /// Pure logic over the warrior upgrade tree (P3-06). Deterministic, no RNG, no
    /// side effects except the explicit ledger debit in <see cref="TryPurchaseTier"/>:
    /// the single place tier costs and stat deltas are interpreted, so the lodge and
    /// the tests agree. Tier 0 is the config base stats; each purchased tier adds its
    /// deltas cumulatively.
    /// </summary>
    public static class WarriorUpgrades
    {
        /// <summary>Base (tier-0) warrior stats straight from config.</summary>
        public static WarriorStats BaseStats(CombatConfig cfg)
        {
            if (cfg == null)
            {
                cfg = CombatConfig.LoadOrDefault();
            }
            return new WarriorStats(
                Mathf.Max(1f, cfg.warriorBaseMaxHealth),
                Mathf.Max(0f, cfg.warriorBaseAttackDamage),
                Mathf.Max(0f, cfg.warriorAttackRange),
                Mathf.Max(0f, cfg.warriorMoveSpeed),
                Mathf.Max(0.01f, cfg.warriorAttackCooldownSeconds),
                Mathf.Clamp01(cfg.warriorBaseDarkSanityDrainFraction));
        }

        /// <summary>
        /// Warrior stats after applying the first <paramref name="tierCount"/> tiers
        /// (clamped to the tree length). Deltas accumulate; damage/health/cooldown are
        /// floored to sane minimums so a mis-authored tree can never produce negatives.
        /// </summary>
        public static WarriorStats StatsAtTier(CombatConfig cfg, int tierCount)
        {
            if (cfg == null)
            {
                cfg = CombatConfig.LoadOrDefault();
            }
            var tiers = cfg.warriorUpgradeTiers;
            int applied = tiers == null ? 0 : Mathf.Clamp(tierCount, 0, tiers.Count);

            float health = cfg.warriorBaseMaxHealth;
            float damage = cfg.warriorBaseAttackDamage;
            float cooldown = cfg.warriorAttackCooldownSeconds;
            float drain = cfg.warriorBaseDarkSanityDrainFraction;

            for (int i = 0; i < applied; i++)
            {
                var tier = tiers[i];
                if (tier == null)
                {
                    continue;
                }
                health += tier.healthDelta;
                damage += tier.damageDelta;
                cooldown += tier.attackCooldownDelta;
                drain -= tier.darkSanityResistDelta;
            }

            return new WarriorStats(
                Mathf.Max(1f, health),
                Mathf.Max(0f, damage),
                Mathf.Max(0f, cfg.warriorAttackRange),
                Mathf.Max(0f, cfg.warriorMoveSpeed),
                Mathf.Max(0.01f, cooldown),
                Mathf.Clamp01(drain));
        }

        /// <summary>The number of tiers in the tree (the max purchasable upgrade level).</summary>
        public static int TierCount(CombatConfig cfg)
        {
            if (cfg == null)
            {
                cfg = CombatConfig.LoadOrDefault();
            }
            return cfg.warriorUpgradeTiers != null ? cfg.warriorUpgradeTiers.Count : 0;
        }

        /// <summary>True when the ledger can afford the tier at <paramref name="tierIndex"/>.</summary>
        public static bool CanAfford(CombatConfig cfg, int tierIndex)
        {
            if (cfg == null)
            {
                cfg = CombatConfig.LoadOrDefault();
            }
            var tiers = cfg.warriorUpgradeTiers;
            if (tiers == null || tierIndex < 0 || tierIndex >= tiers.Count || tiers[tierIndex] == null)
            {
                return false;
            }
            return ResourceLedger.CanAfford(tiers[tierIndex].cost);
        }

        /// <summary>
        /// Atomically purchases the tier at <paramref name="tierIndex"/>: debits its
        /// cost from the ledger and, on success, logs the purchase. False (ledger
        /// untouched) when the index is out of range or the settlement cannot afford
        /// it. The caller advances its applied-tier counter only on a true return.
        /// </summary>
        public static bool TryPurchaseTier(CombatConfig cfg, int tierIndex, string reason)
        {
            if (cfg == null)
            {
                cfg = CombatConfig.LoadOrDefault();
            }
            var tiers = cfg.warriorUpgradeTiers;
            if (tiers == null || tierIndex < 0 || tierIndex >= tiers.Count || tiers[tierIndex] == null)
            {
                return false;
            }
            var tier = tiers[tierIndex];
            if (!ResourceLedger.TryConsume(tier.cost, reason ?? $"warrior_upgrade:{tier.id}"))
            {
                return false;
            }
            Abbey.Core.GameEventLog.Append("warrior_upgrade",
                $"tier={tierIndex + 1} id={tier.id}");
            return true;
        }

        /// <summary>
        /// The coded default upgrade tree (overridden by any CombatConfig asset). Two
        /// tiers spending the Phase 3 crafted economy (tools/scrap_iron/coal): drilled
        /// blades (more damage, faster) then warded plate (more health, dark grit).
        /// </summary>
        public static List<WarriorUpgradeTier> DefaultTiers()
        {
            return new List<WarriorUpgradeTier>
            {
                new WarriorUpgradeTier
                {
                    id = "drilled_blades",
                    displayName = "Drilled Blades",
                    cost = new List<ResourceStack>
                    {
                        new ResourceStack(ResourceType.Tools, 2),
                        new ResourceStack(ResourceType.ScrapIron, 3),
                    },
                    damageDelta = 6f,
                    attackCooldownDelta = -0.1f,
                },
                new WarriorUpgradeTier
                {
                    id = "warded_plate",
                    displayName = "Warded Plate",
                    cost = new List<ResourceStack>
                    {
                        new ResourceStack(ResourceType.ScrapIron, 4),
                        new ResourceStack(ResourceType.Coal, 3),
                    },
                    healthDelta = 40f,
                    darkSanityResistDelta = 0.25f,
                },
            };
        }
    }
}
