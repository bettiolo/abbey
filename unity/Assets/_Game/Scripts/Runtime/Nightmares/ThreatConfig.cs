using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abbey.Nightmares
{
    /// <summary>
    /// The seven exploitable places on the map (ROADMAP Phase 3 item 10). Each accumulates
    /// exploitation pressure as the settlement cuts, draws, mines, salvages, buries or
    /// overuses it, and the <see cref="NightmareDirector"/> weights spawn placement toward
    /// the most-pressured sources.
    /// </summary>
    public enum ThreatSourceType
    {
        Forest,   // woodcutting
        Well,     // drawing water / daily rations
        Cave,     // coal mining
        Mountain, // stone quarrying
        Shore,    // shipwreck salvage
        Crypt,    // grave handling
        OldRoad   // heavy hauling traffic
    }

    /// <summary>
    /// The single ScriptableObject holding ALL P3-11 balance (AGENTS.md: no magnitudes in
    /// MonoBehaviours). Mirrors <see cref="Abbey.Morale.PressuresConfig"/> /
    /// <see cref="Abbey.Decrees.LawsConfig"/>: fetched through <see cref="LoadOrDefault"/> so
    /// tests and CI never need an asset; an optional asset at Resources/ThreatConfig overrides
    /// the coded defaults.
    ///
    /// Holds the five consequence-nightmare trigger rules (their tag / pressure conditions,
    /// spawn counts, preferred source and per-type monster stats), the event→source
    /// exploitation-pressure map the <see cref="ThreatSourceSystem"/> folds, and the source
    /// decay / mitigation / spawn-weighting tunables.
    /// </summary>
    [CreateAssetMenu(fileName = "ThreatConfig", menuName = "Abbey/Threat Config")]
    public class ThreatConfig : ScriptableObject
    {
        public const string ResourcePath = "ThreatConfig";

        [Header("Consequence nightmare triggers (one rule per species)")]
        public List<ConsequenceTriggerRule> triggers = DefaultTriggers();

        [Header("Exploitation: event → source pressure the fold applies")]
        public List<ExploitationMapping> exploitation = DefaultExploitation();

        [Header("Source pressure decay / clamp")]
        [Tooltip("How far each source is pulled back toward 0 each elapsed day (day marker).")]
        [Min(0f)] public float sourceDecayPerDay = 0.05f;

        [Tooltip("Upper clamp on any single source's accumulated pressure.")]
        [Min(0f)] public float maxSourcePressure = 3f;

        [Header("Day marker (a decay step per matching log record)")]
        public string dayMarkerEventType = "RationsIssued";
        public string dayMarkerDataContains = "law=";

        [Header("Mitigation (rest a source / offerings / reconsecration)")]
        [Tooltip("Log record type the fold reads as a mitigation (data 'source=<x> amount=<f>').")]
        public string mitigationEventType = "threat_mitigation";

        [Header("Spawn weighting")]
        [Tooltip("Floor weight every registered source keeps so selection is defined at zero pressure.")]
        [Min(0f)] public float sourceWeightFloor = 0.05f;

        [Tooltip("Extra weight a source gets when it matches the arming trigger's preferred source.")]
        [Min(0f)] public float preferredSourceBonus = 0.75f;

        // ------------------------------------------------------------------
        // Lookups
        // ------------------------------------------------------------------

        public ConsequenceTriggerRule RuleFor(NightmareType type)
        {
            if (triggers != null)
            {
                for (int i = 0; i < triggers.Count; i++)
                {
                    if (triggers[i] != null && triggers[i].type == type)
                    {
                        return triggers[i];
                    }
                }
            }
            return new ConsequenceTriggerRule { type = type };
        }

        // ------------------------------------------------------------------
        // Coded defaults
        // ------------------------------------------------------------------

        static List<ConsequenceTriggerRule> DefaultTriggers()
        {
            return new List<ConsequenceTriggerRule>
            {
                // Hunger Wights — the starved dead. Armed by the Fasting food law OR high
                // Hunger pressure. They gather at the well/fields the settlement drains.
                new ConsequenceTriggerRule
                {
                    type = NightmareType.HungerWight,
                    activeTagAny = new[] { "fasting_active" },
                    hungerAtLeast = 0.5f,
                    spawnCount = 2,
                    preferredSource = ThreatSourceType.Well,
                    healthScale = 0.7f,
                    stunnedByBell = true,
                },
                // Dead Workers — those worked to death after the bell. Armed only while the
                // Forced night-labour law stands AND a death has been logged.
                new ConsequenceTriggerRule
                {
                    type = NightmareType.DeadWorker,
                    activeTagAny = new[] { "forced_labour_night" },
                    requireVillagerDeath = true,
                    spawnCount = 2,
                    preferredSource = ThreatSourceType.Mountain,
                    healthScale = 1.1f,
                    stunnedByBell = false,
                },
                // Grave Crawlers — what claws out of a mass grave / a body put to use. Armed
                // by the per-death grave tags Mass Graves / Use the Dead stamp in the log.
                new ConsequenceTriggerRule
                {
                    type = NightmareType.GraveCrawler,
                    logTagAny = new[] { "grave_mass", "grave_used" },
                    spawnCount = 2,
                    preferredSource = ThreatSourceType.Crypt,
                    healthScale = 1.0f,
                    stunnedByBell = true,
                },
                // Chain Hounds — the hound doctrine turned monstrous. Armed by the Chained
                // hound law OR a Broken abbey. They come up the old roads.
                new ConsequenceTriggerRule
                {
                    type = NightmareType.ChainHound,
                    activeTagAny = new[] { "hound_chained" },
                    armOnBrokenForm = true,
                    spawnCount = 1,
                    preferredSource = ThreatSourceType.OldRoad,
                    healthScale = 1.3f,
                    stunnedByBell = false,
                },
                // Faceless Saints — the old faith's answer to forbidden rites. Armed while
                // pagan rites are forbidden AND Old-faith runs high OR Sanctity has collapsed.
                new ConsequenceTriggerRule
                {
                    type = NightmareType.FacelessSaint,
                    activeTagAny = new[] { "pagan_rites_forbidden" },
                    oldFaithAtLeast = 0.4f,
                    sanctityAtMost = 0.3f,
                    spawnCount = 1,
                    preferredSource = ThreatSourceType.Shore,
                    healthScale = 1.5f,
                    stunnedByBell = false,
                },
            };
        }

        static List<ExploitationMapping> DefaultExploitation()
        {
            return new List<ExploitationMapping>
            {
                // Resource ledger records read "wood +N (reason)", "coal +N", etc.
                Map("wood +", true, ThreatSourceType.Forest, 0.10f),
                Map("stone +", true, ThreatSourceType.Mountain, 0.12f),
                Map("coal +", true, ThreatSourceType.Cave, 0.14f),
                Map("scrap_iron +", true, ThreatSourceType.Shore, 0.12f),
                // Shipwreck salvage stage advances.
                Map("salvage", false, ThreatSourceType.Shore, 0.10f),
                // Grave handling — every burial disturbs the crypt.
                Map("burial", false, ThreatSourceType.Crypt, 0.18f),
                // Drawing the well for the daily ration pass.
                Map("RationsIssued", false, ThreatSourceType.Well, 0.06f),
                // Heavy hauling wears the old roads.
                Map("villager_deposited_resource", false, ThreatSourceType.OldRoad, 0.05f),
            };
        }

        static ExploitationMapping Map(string signal, bool matchData, ThreatSourceType source, float rate)
        {
            return new ExploitationMapping
            {
                signal = signal,
                matchData = matchData,
                source = source,
                pressurePerEvent = rate,
            };
        }

        static ThreatConfig _cached;

        /// <summary>Resources asset if present, otherwise a coded-default instance. Never null.</summary>
        public static ThreatConfig LoadOrDefault()
        {
            if (_cached != null)
            {
                return _cached;
            }
            _cached = Resources.Load<ThreatConfig>(ResourcePath);
            if (_cached == null)
            {
                _cached = CreateInstance<ThreatConfig>();
                _cached.name = "ThreatConfig (defaults)";
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

    /// <summary>
    /// One consequence-nightmare trigger rule. The arming CONDITION structure is fixed per
    /// species (see <see cref="ConsequenceNightmareCatalog"/>); the THRESHOLDS, tags, spawn
    /// count, preferred source and monster stats are all data here.
    /// </summary>
    [Serializable]
    public class ConsequenceTriggerRule
    {
        public NightmareType type = NightmareType.HungerWight;

        [Tooltip("Armed when any of these standing law tags is active.")]
        public string[] activeTagAny = Array.Empty<string>();

        [Tooltip("Armed when any of these tags appears in the event log (e.g. per-death grave tags).")]
        public string[] logTagAny = Array.Empty<string>();

        [Tooltip("Hunger pressure at/above this arms the rule (negative = ignore).")]
        public float hungerAtLeast = -1f;

        [Tooltip("Old-faith pressure at/above this contributes to arming (negative = ignore).")]
        public float oldFaithAtLeast = -1f;

        [Tooltip("Sanctity pressure at/below this contributes to arming (>1 = ignore).")]
        public float sanctityAtMost = 2f;

        [Tooltip("Armed when the abbey has transformed to Broken.")]
        public bool armOnBrokenForm;

        [Tooltip("Requires a villager death in the log (with the active tag) — Dead Workers.")]
        public bool requireVillagerDeath;

        [Header("Spawn")]
        [Min(0)] public int spawnCount = 1;
        public ThreatSourceType preferredSource = ThreatSourceType.Forest;

        [Header("Monster stats")]
        [Tooltip("Multiplier on monsterMaxHealth for this species.")]
        [Min(0.01f)] public float healthScale = 1f;

        [Tooltip("Whether a bell pulse stuns this species (weak nightmares are stunned).")]
        public bool stunnedByBell = true;
    }

    /// <summary>
    /// One event→source mapping. When <see cref="matchData"/> is false the fold matches the
    /// record TYPE against <see cref="signal"/>; when true it matches records whose DATA
    /// contains the signal. Each match adds <see cref="pressurePerEvent"/> to the source.
    /// </summary>
    [Serializable]
    public class ExploitationMapping
    {
        public string signal = "";
        public bool matchData;
        public ThreatSourceType source = ThreatSourceType.Forest;
        public float pressurePerEvent = 0.1f;
    }
}
