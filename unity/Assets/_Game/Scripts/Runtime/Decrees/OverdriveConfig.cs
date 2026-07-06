using System;
using System.Collections.Generic;
using Abbey.Economy;
using UnityEngine;

namespace Abbey.Decrees
{
    /// <summary>
    /// The immediate + deferred costs and effect parameters of one overdrive lever
    /// (AGENTS.md: no balance inside MonoBehaviours). A lever spends some of its cost now
    /// — resources debited from the <see cref="Abbey.Economy.ResourceLedger"/>, a sharp
    /// sanity hit on each participant, a trust / beast-status delta — and books the rest
    /// as deferred debt: a dread the sanity system adds to each participant at dawn, and
    /// nightmare-debt points the <c>NightmareDirector</c> reads on later nights.
    /// </summary>
    [Serializable]
    public class OverdriveActionDef
    {
        public OverdriveActionId id = OverdriveActionId.ForcedNightWork;

        [Header("Resource costs")]
        [Tooltip("Paid once when the lever is fired (candles to hand out, oil to prime).")]
        public List<ResourceStack> immediateCost = new List<ResourceStack>();

        [Tooltip("Drained every upkeepIntervalSeconds while the lever is active (0 = no upkeep).")]
        public ResourceType upkeepType = ResourceType.Candles;

        [Min(0)] public int upkeepAmount;

        [Tooltip("Seconds between upkeep drains (an in-game hour by default).")]
        [Min(0.01f)] public float upkeepIntervalSeconds = 3600f;

        [Header("Sanity")]
        [Tooltip("Immediate sanity cost applied to every participating villager on activation.")]
        [Range(0f, 1f)] public float sanityCostPerVillager;

        [Tooltip("Dread the sanity system adds to each participant at dawn (the deferred toll).")]
        [Range(0f, 1f)] public float deferredDreadPerVillager;

        [Header("Social pressure (signed deltas into the P3-10 stub ledgers)")]
        public float trustDelta;
        public float beastStatusDelta;

        [Header("Deferred nightmare debt (settled at dawn, spent by the director later)")]
        [Min(0f)] public float nightmareDebtPoints;

        [Header("Lifecycle")]
        [Tooltip("Nights that must pass before the lever can be fired again (0 = every night).")]
        [Min(0)] public int cooldownNights;

        [Tooltip("Participants skip dusk recall (kept out working); handed back at dawn.")]
        public bool exemptsFromRecall;

        [Header("Candle Line")]
        [Tooltip("How many villagers become candle carriers (mobile lights along the route).")]
        [Min(0)] public int candleCarriers;

        [Min(0f)] public float carrierLightRadius = 4f;

        [Range(0f, 1f)] public float carrierLightStrength = 1f;

        [Header("Lantern Overburn")]
        [Min(1f)] public float overburnRadiusMultiplier = 1f;

        [Min(1f)] public float overburnFuelMultiplier = 1f;

        [Header("Bell Toll")]
        [Tooltip("Radius of the recall/hesitation bell pulse this lever rings (0 = none).")]
        [Min(0f)] public float bellRadius;
    }

    /// <summary>
    /// The single ScriptableObject holding ALL overdrive balance (AGENTS.md). Mirrors
    /// <see cref="Abbey.Beast.HoundEvolutionConfig"/>: fetched via
    /// <see cref="LoadOrDefault"/> so tests and CI never need an asset; an optional asset
    /// at Resources/OverdriveConfig overrides the coded defaults. Also holds the deferred
    /// nightmare-debt → extra-monster conversion the director consumes on later nights.
    /// </summary>
    [CreateAssetMenu(fileName = "OverdriveConfig", menuName = "Abbey/Overdrive Config")]
    public class OverdriveConfig : ScriptableObject
    {
        public const string ResourcePath = "OverdriveConfig";

        [Header("Deferred nightmare debt → director intensity")]
        [Tooltip("Extra monsters a later night spawns per point of accumulated debt.")]
        [Min(0f)] public float debtToMonsterFactor = 0.5f;

        [Tooltip("Hard cap on debt-driven extra monsters in any single night.")]
        [Min(0)] public int maxDebtMonstersPerNight = 12;

        [Tooltip("Fraction of the pending debt a night burns off when it cashes it in (1 = all).")]
        [Range(0f, 1f)] public float debtDrainFraction = 1f;

        [Tooltip("Per-action cost + effect definitions (one per OverdriveActionId). Data.")]
        public List<OverdriveActionDef> actions = DefaultActions();

        /// <summary>The definition for an action, or a coded-neutral default when absent.</summary>
        public OverdriveActionDef DefFor(OverdriveActionId id)
        {
            if (actions != null)
            {
                for (int i = 0; i < actions.Count; i++)
                {
                    if (actions[i] != null && actions[i].id == id)
                    {
                        return actions[i];
                    }
                }
            }
            return new OverdriveActionDef { id = id };
        }

        /// <summary>Debt-driven extra monsters for a night, and the debt actually consumed.</summary>
        public int DebtMonsters(float pendingDebt, out float consumedDebt)
        {
            consumedDebt = 0f;
            if (pendingDebt <= 0f)
            {
                return 0;
            }
            int extra = Mathf.Clamp(
                Mathf.CeilToInt(pendingDebt * Mathf.Max(0f, debtToMonsterFactor)),
                0, Mathf.Max(0, maxDebtMonstersPerNight));
            consumedDebt = pendingDebt * Mathf.Clamp01(debtDrainFraction);
            return extra;
        }

        /// <summary>
        /// The coded default levers (used when no asset is present). Values sketch the
        /// seven emergency actions from ROADMAP Phase 3 item 16 — panic buttons that
        /// solve tonight and book a bill for later.
        /// </summary>
        public static List<OverdriveActionDef> DefaultActions()
        {
            return new List<OverdriveActionDef>
            {
                // A job site works the night: it costs the workers dearly (sanity + a
                // deferred dread) and the settlement's trust, and books a heavy debt.
                new OverdriveActionDef
                {
                    id = OverdriveActionId.ForcedNightWork,
                    sanityCostPerVillager = 0.12f,
                    deferredDreadPerVillager = 0.15f,
                    trustDelta = -0.15f,
                    nightmareDebtPoints = 3f,
                    exemptsFromRecall = true
                },
                // Candle carriers light a route: candles up front + candles per hour,
                // a small sanity cost, a light debt.
                new OverdriveActionDef
                {
                    id = OverdriveActionId.CandleLine,
                    immediateCost = new List<ResourceStack> { new ResourceStack(ResourceType.Candles, 3) },
                    upkeepType = ResourceType.Candles,
                    upkeepAmount = 1,
                    upkeepIntervalSeconds = 3600f,
                    sanityCostPerVillager = 0.05f,
                    deferredDreadPerVillager = 0.05f,
                    nightmareDebtPoints = 1f,
                    exemptsFromRecall = true,
                    candleCarriers = 4,
                    carrierLightRadius = 4f,
                    carrierLightStrength = 1f
                },
                // Overburn lanterns: prime with oil, then they burn brighter and faster
                // on their own fuel; a risk tag if they gutter out mid-night.
                new OverdriveActionDef
                {
                    id = OverdriveActionId.LanternOverburn,
                    immediateCost = new List<ResourceStack> { new ResourceStack(ResourceType.Oil, 2) },
                    nightmareDebtPoints = 1f,
                    overburnRadiusMultiplier = 1.6f,
                    overburnFuelMultiplier = 2.5f
                },
                // Toll the bell: recall boost + monster hesitation, trust erodes if leaned on.
                new OverdriveActionDef
                {
                    id = OverdriveActionId.BellToll,
                    trustDelta = -0.05f,
                    nightmareDebtPoints = 0.5f,
                    cooldownNights = 0,
                    bellRadius = 40f
                },
                // Abbey rite: burn candles for a calm aura; books old-faith / nightmare tags.
                new OverdriveActionDef
                {
                    id = OverdriveActionId.AbbeyRite,
                    immediateCost = new List<ResourceStack> { new ResourceStack(ResourceType.Candles, 4) },
                    deferredDreadPerVillager = -0.02f,
                    nightmareDebtPoints = 4f
                },
                // Send the hound to hunt: it costs the settlement's standing with the beast
                // and risks the hound (pain/fear feed P3-07); a modest debt.
                new OverdriveActionDef
                {
                    id = OverdriveActionId.HoundHunt,
                    beastStatusDelta = -0.2f,
                    nightmareDebtPoints = 1.5f,
                    cooldownNights = 1
                },
                // Volunteer watch: trusted villagers stand armed at a sanity cost.
                new OverdriveActionDef
                {
                    id = OverdriveActionId.VolunteerWatch,
                    sanityCostPerVillager = 0.08f,
                    deferredDreadPerVillager = 0.08f,
                    trustDelta = 0.05f,
                    nightmareDebtPoints = 1f,
                    exemptsFromRecall = true
                },
            };
        }

        static OverdriveConfig _cached;

        /// <summary>Resources asset if present, otherwise a coded-default instance. Never null.</summary>
        public static OverdriveConfig LoadOrDefault()
        {
            if (_cached != null)
            {
                return _cached;
            }
            _cached = Resources.Load<OverdriveConfig>(ResourcePath);
            if (_cached == null)
            {
                _cached = CreateInstance<OverdriveConfig>();
                _cached.name = "OverdriveConfig (defaults)";
                _cached.hideFlags = HideFlags.HideAndDontSave;
            }
            return _cached;
        }

        /// <summary>Drops the cached instance (test isolation).</summary>
        public static void ClearCache()
        {
            _cached = null;
        }
    }
}
