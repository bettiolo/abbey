using System;
using System.Collections.Generic;
using Abbey.Economy;
using UnityEngine;

namespace Abbey.Map2
{
    /// <summary>
    /// All balance and content tables for the Abbey of Antlers.  The Stag system,
    /// campaign carryover, dilemma cadence and both victory routes read this one
    /// ScriptableObject; Map-2 MonoBehaviours contain no hidden magnitudes.
    /// </summary>
    [CreateAssetMenu(fileName = "Map2Config", menuName = "Abbey/Map 2 Config")]
    public class Map2Config : ScriptableObject
    {
        public const string ResourcePath = "Map2Config";

        [Header("Stag baseline (0..1)")]
        [Range(0f, 1f)] public float startingTrust = 0.28f;
        [Range(0f, 1f)] public float startingPatience = 0.62f;
        [Range(0f, 1f)] public float startingWound = 0.72f;
        [Range(0f, 1f)] public float startingWildness = 0.68f;
        [Range(0f, 1f)] public float startingCovenant = 0.55f;

        [Header("Stag state thresholds")]
        [Range(0f, 1f)] public float brokenCovenantAt = 0.12f;
        [Range(0f, 1f)] public float waryPatienceAt = 0.24f;
        [Range(0f, 1f)] public float waryWildnessAt = 0.82f;
        [Range(0f, 1f)] public float permittingTrustAt = 0.48f;
        [Range(0f, 1f)] public float permittingCovenantAt = 0.48f;
        [Range(0f, 1f)] public float alliedTrustAt = 0.72f;
        [Range(0f, 1f)] public float alliedCovenantAt = 0.75f;

        [Header("Indirect interactions")]
        public List<StagInteractionDefinition> interactions = DefaultInteractions();
        public List<StagReactionDefinition> reactions = DefaultReactions();

        [Header("Map-1 trait carryover")]
        [Min(0f)] public float calmingPresenceTrustBonus = 0.08f;
        [Range(0f, 1f)] public float calmingPresenceFalseBellFearMultiplier = 0.65f;
        [Min(0f)] public float commandingVoiceBellRadiusMultiplier = 1.25f;
        [Min(0f)] public float ritualAuthorityCovenantBonus = 0.10f;
        [Min(0f)] public float hardLessonsDebtGrace = 0.35f;

        [Header("Story cadence")]
        public string[] dilemmaIds =
        {
            "old_tree", "starving_deer", "lost_woodcutters", "charcoal_camp",
        };
        public int[] dilemmaDays = { 2, 4, 6, 8 };

        [Header("Shared victory gate")]
        [Min(0)] public int minimumNightsSurvived = 3;

        [Header("Covenant victory")]
        [Min(0f)] public float covenantRouteMaxForestDebt = 0.65f;
        public List<ResourceStack> covenantRouteStock = new List<ResourceStack>
        {
            new ResourceStack(ResourceType.SacredSeeds, 4),
            new ResourceStack(ResourceType.Herbs, 5),
            new ResourceStack(ResourceType.Resin, 3),
        };

        [Header("Exploitative victory (profitable, but covenant must not break)")]
        [Min(0f)] public float exploitativeRouteMaxForestDebt = 2.45f;
        public List<ResourceStack> exploitativeRouteStock = new List<ResourceStack>
        {
            new ResourceStack(ResourceType.OldWood, 10),
            new ResourceStack(ResourceType.Charcoal, 8),
            new ResourceStack(ResourceType.Venison, 6),
        };

        public StagInteractionDefinition InteractionFor(string id)
        {
            if (interactions == null || string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < interactions.Count; i++)
            {
                var item = interactions[i];
                if (item != null && item.id == id) return item;
            }
            return null;
        }

        static List<StagInteractionDefinition> DefaultInteractions()
        {
            return new List<StagInteractionDefinition>
            {
                new StagInteractionDefinition
                {
                    id = "observe", signal = "observe_stag",
                    costs = new List<ResourceStack>(),
                },
                new StagInteractionDefinition
                {
                    id = "leave_offering", signal = "leave_offering",
                    costs = new List<ResourceStack>
                    {
                        new ResourceStack(ResourceType.Apples, 1),
                        new ResourceStack(ResourceType.Herbs, 1),
                    },
                },
                new StagInteractionDefinition
                {
                    id = "tend_wound", signal = "heal_stag",
                    costs = new List<ResourceStack>
                    {
                        new ResourceStack(ResourceType.Herbs, 2),
                        new ResourceStack(ResourceType.Resin, 1),
                    },
                },
                new StagInteractionDefinition
                {
                    id = "follow_sign", signal = "follow_deer_path",
                    costs = new List<ResourceStack>(),
                },
            };
        }

        static List<StagReactionDefinition> DefaultReactions()
        {
            return new List<StagReactionDefinition>
            {
                React("observe_stag", trust: 0.03f, patience: 0.08f, wildness: -0.02f),
                React("leave_offering", trust: 0.12f, patience: 0.06f, covenant: 0.10f),
                React("heal_stag", trust: 0.13f, patience: 0.05f, wound: -0.18f, covenant: 0.08f),
                React("follow_deer_path", trust: 0.05f, patience: 0.07f, covenant: 0.03f),
                React("replanting", trust: 0.06f, patience: 0.05f, wildness: -0.02f, covenant: 0.09f),
                React("grove_shrine", trust: 0.05f, patience: 0.08f, covenant: 0.12f),
                React("deer_protected", trust: 0.06f, patience: 0.04f, covenant: 0.08f),
                React("tree_burial", trust: 0.03f, patience: 0.05f, covenant: 0.08f),
                React("forest_restraint", patience: 0.06f, wildness: -0.02f, covenant: 0.06f),
                React("old_growth_cutting", trust: -0.08f, patience: -0.12f, wildness: 0.05f, covenant: -0.11f),
                React("overhunting", trust: -0.06f, patience: -0.10f, wildness: 0.08f, covenant: -0.12f),
                React("grove_intrusion", trust: -0.10f, patience: -0.13f, wildness: 0.07f, covenant: -0.14f),
                React("night_burning", trust: -0.08f, patience: -0.12f, wound: 0.04f, covenant: -0.15f),
                React("forced_forest_labour", trust: -0.07f, patience: -0.10f, covenant: -0.13f),
                React("covenant_broken", trust: -1f, patience: -1f, wildness: 1f, covenant: -1f),
            };
        }

        static StagReactionDefinition React(
            string signal, float trust = 0f, float patience = 0f, float wound = 0f,
            float wildness = 0f, float covenant = 0f)
        {
            return new StagReactionDefinition
            {
                signal = signal,
                trustDelta = trust,
                patienceDelta = patience,
                woundDelta = wound,
                wildnessDelta = wildness,
                covenantDelta = covenant,
            };
        }

        static Map2Config _cached;

        public static Map2Config LoadOrDefault()
        {
            if (_cached != null) return _cached;
            _cached = Resources.Load<Map2Config>(ResourcePath);
            if (_cached == null)
            {
                _cached = CreateInstance<Map2Config>();
                _cached.name = "Map2Config (defaults)";
                _cached.hideFlags = HideFlags.HideAndDontSave;
            }
            return _cached;
        }

        public static void ClearCache() => _cached = null;
    }

    [Serializable]
    public class StagInteractionDefinition
    {
        public string id = string.Empty;
        public string signal = string.Empty;
        public List<ResourceStack> costs = new List<ResourceStack>();
    }

    [Serializable]
    public class StagReactionDefinition
    {
        public string signal = string.Empty;
        public float trustDelta;
        public float patienceDelta;
        public float woundDelta;
        public float wildnessDelta;
        public float covenantDelta;
    }
}
