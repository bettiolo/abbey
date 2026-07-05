using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abbey.Beast
{
    /// <summary>
    /// The long-arc evolution paths of the Black Hound (ROADMAP Phase 3 item 4). The
    /// Phase 2 bond (trust/hunger/pain/fear/attachment) is moment-to-moment mood; the
    /// path is how a year of TREATMENT hardens the hound into a role that changes its
    /// night behaviour and how people feel about it.
    /// <see cref="Unevolved"/> is the neutral default before any path locks in.
    /// </summary>
    public enum HoundPath
    {
        Unevolved,
        Guardian,
        War,
        Starved,
        Sacred,
        Broken
    }

    /// <summary>
    /// The settlement's standing policy toward the hound. Written by the Hound law in
    /// P3-09 (Family / Weapon / Chained / Sacred); until then it stays
    /// <see cref="Neutral"/> and the path is decided by treatment + bond alone. Biases
    /// the path scores through the config's per-profile doctrine terms.
    /// </summary>
    public enum HoundDoctrine
    {
        Neutral,
        Family,
        Weapon,
        Chained,
        Sacred
    }

    /// <summary>
    /// A snapshot of how the settlement has treated the hound plus its current bond
    /// averages — the pure input to path scoring. Built by
    /// <see cref="HoundController.BuildTreatmentSample"/>; tests construct it directly
    /// to drive a scripted history into <see cref="HoundEvolutionConfig"/>.
    /// </summary>
    public readonly struct HoundTreatmentSample
    {
        public readonly int FeedEvents;
        public readonly int AlliedFights;
        public readonly int SoloHunts;
        public readonly int Rites;
        public readonly float ChainMinutes;
        public readonly int Injuries;
        public readonly float Trust;
        public readonly float Hunger;
        public readonly float Pain;
        public readonly float Fear;
        public readonly float Attachment;

        public HoundTreatmentSample(int feedEvents, int alliedFights, int soloHunts, int rites,
            float chainMinutes, int injuries, float trust, float hunger, float pain, float fear,
            float attachment)
        {
            FeedEvents = feedEvents;
            AlliedFights = alliedFights;
            SoloHunts = soloHunts;
            Rites = rites;
            ChainMinutes = chainMinutes;
            Injuries = injuries;
            Trust = trust;
            Hunger = hunger;
            Pain = pain;
            Fear = fear;
            Attachment = attachment;
        }
    }

    /// <summary>
    /// The behaviour parameter set a path imposes on the <see cref="HoundController"/>
    /// (data, not code — AGENTS.md). Only the load-bearing knobs are wired into the
    /// controller; the rest are read by the debug overlay and downstream tasks.
    /// </summary>
    [Serializable]
    public class HoundBehaviourParams
    {
        [Tooltip("Scales the hound's outgoing bite damage (War hits harder; Broken/Starved less).")]
        [Min(0f)] public float aggressionMultiplier = 1f;

        [Tooltip("Bell obedience: 1 answers normally, 0 refuses the bell entirely (Starved/Broken).")]
        [Range(0f, 1f)] public float bellResponseMultiplier = 1f;

        [Tooltip("How close the hound likes to settle to villagers (Guardian sits among them). Display + downstream.")]
        [Min(0f)] public float villagerComfortRadius = 0f;

        [Tooltip("Starved: the hound hunts alone and drags kills into the dark rather than defending.")]
        public bool huntsAlone = false;

        [Tooltip("Sacred: the hound will not fight except within a sacred light.")]
        public bool fightsOnlyAtAbbey = false;

        [Tooltip("Broken: cowed or vicious and unreliable — may turn (read by pressures/summary).")]
        public bool unreliable = false;
    }

    /// <summary>
    /// Per-path scoring weights + behaviour + beast-status output. One profile per
    /// <see cref="HoundPath"/>. A path's score is a weighted sum of the treatment
    /// counters and bond averages plus a doctrine bias; the dominant score over the
    /// config threshold adopts (and eventually locks) the path.
    /// </summary>
    [Serializable]
    public class HoundPathProfile
    {
        public HoundPath path = HoundPath.Unevolved;

        [Header("Treatment weights")]
        public float feedWeight;
        public float alliedFightWeight;
        public float soloHuntWeight;
        public float riteWeight;
        [Tooltip("Per accumulated chain-minute.")]
        public float chainWeight;
        public float injuryWeight;

        [Header("Bond weights (applied to the bond averages 0..1)")]
        public float trustWeight;
        public float hungerWeight;
        public float painWeight;
        public float fearWeight;
        public float attachmentWeight;

        [Header("Doctrine bias (added when the matching Hound law is in force)")]
        public float doctrineFamilyBias;
        public float doctrineWeaponBias;
        public float doctrineChainedBias;
        public float doctrineSacredBias;

        [Header("Behaviour this path imposes")]
        public HoundBehaviourParams behaviour = new HoundBehaviourParams();

        [Header("Beast status this path yields (-1 feared .. +1 beloved)")]
        [Range(-1f, 1f)] public float beastStatusBase;
        [Tooltip("How much (avgTrust - avgFear) nudges the base standing.")]
        [Range(0f, 1f)] public float beastStatusBondWeight = 0.3f;

        public float DoctrineBias(HoundDoctrine doctrine)
        {
            switch (doctrine)
            {
                case HoundDoctrine.Family: return doctrineFamilyBias;
                case HoundDoctrine.Weapon: return doctrineWeaponBias;
                case HoundDoctrine.Chained: return doctrineChainedBias;
                case HoundDoctrine.Sacred: return doctrineSacredBias;
                default: return 0f;
            }
        }

        public float Score(in HoundTreatmentSample s, HoundDoctrine doctrine)
        {
            return feedWeight * s.FeedEvents
                   + alliedFightWeight * s.AlliedFights
                   + soloHuntWeight * s.SoloHunts
                   + riteWeight * s.Rites
                   + chainWeight * s.ChainMinutes
                   + injuryWeight * s.Injuries
                   + trustWeight * s.Trust
                   + hungerWeight * s.Hunger
                   + painWeight * s.Pain
                   + fearWeight * s.Fear
                   + attachmentWeight * s.Attachment
                   + DoctrineBias(doctrine);
        }

        /// <summary>Beast standing (-1..+1) on this path, nudged by the current bond.</summary>
        public float BeastStatus(float avgTrust, float avgFear)
        {
            return Mathf.Clamp(beastStatusBase + beastStatusBondWeight * (avgTrust - avgFear), -1f, 1f);
        }
    }

    /// <summary>
    /// The single ScriptableObject holding ALL hound-evolution balance (AGENTS.md: no
    /// balance inside MonoBehaviours). Mirrors <see cref="Abbey.Combat.CombatConfig"/>:
    /// fetched via <see cref="LoadOrDefault"/> so tests and CI never need an asset; an
    /// optional asset at Resources/HoundEvolutionConfig overrides the coded defaults.
    /// The P2-05 bond thresholds stay in PrototypeConfig — evolution keeps its own
    /// config and does not migrate them.
    /// </summary>
    [CreateAssetMenu(fileName = "HoundEvolutionConfig", menuName = "Abbey/Hound Evolution Config")]
    public class HoundEvolutionConfig : ScriptableObject
    {
        public const string ResourcePath = "HoundEvolutionConfig";

        [Tooltip("Minimum dominant score for the hound to adopt a path (below it stays Unevolved).")]
        [Min(0f)] public float pathAdoptThreshold = 3f;

        [Tooltip("Dominant score at/above which the current path locks in permanently (the year hardened it).")]
        [Min(0f)] public float pathLockThreshold = 8f;

        [Tooltip("Ordered path profiles (one per HoundPath except Unevolved). Weights are data.")]
        public List<HoundPathProfile> profiles = DefaultProfiles();

        /// <summary>The profile for a path, or a neutral Unevolved profile when absent.</summary>
        public HoundPathProfile ProfileFor(HoundPath path)
        {
            if (profiles != null)
            {
                for (int i = 0; i < profiles.Count; i++)
                {
                    if (profiles[i] != null && profiles[i].path == path)
                    {
                        return profiles[i];
                    }
                }
            }
            return _neutral;
        }

        public float ScoreFor(HoundPath path, in HoundTreatmentSample sample, HoundDoctrine doctrine)
        {
            return ProfileFor(path).Score(sample, doctrine);
        }

        /// <summary>
        /// The highest-scoring path and its score for a treatment sample + doctrine.
        /// Ties resolve toward the earlier profile in the list (deterministic).
        /// </summary>
        public HoundPath DominantPath(in HoundTreatmentSample sample, HoundDoctrine doctrine,
            out float score)
        {
            var best = HoundPath.Unevolved;
            score = float.NegativeInfinity;
            if (profiles == null)
            {
                score = 0f;
                return best;
            }
            for (int i = 0; i < profiles.Count; i++)
            {
                var p = profiles[i];
                if (p == null || p.path == HoundPath.Unevolved)
                {
                    continue;
                }
                float s = p.Score(sample, doctrine);
                if (s > score)
                {
                    score = s;
                    best = p.path;
                }
            }
            if (float.IsNegativeInfinity(score))
            {
                score = 0f;
            }
            return best;
        }

        static readonly HoundPathProfile _neutral = new HoundPathProfile
        {
            path = HoundPath.Unevolved,
            beastStatusBase = 0f,
            beastStatusBondWeight = 0.3f,
            behaviour = new HoundBehaviourParams()
        };

        /// <summary>
        /// The coded default profiles (used when no asset is present). Weights sketch the
        /// five paths from ROADMAP Phase 3 item 4:
        /// Guardian (fed + fights alongside + trust), War (constant combat + fed, feared),
        /// Starved (neglect + hunger + solo hunts), Sacred (rites + Sacred doctrine + calm),
        /// Broken (chained + pain + fear + beatings).
        /// </summary>
        public static List<HoundPathProfile> DefaultProfiles()
        {
            return new List<HoundPathProfile>
            {
                new HoundPathProfile
                {
                    path = HoundPath.Guardian,
                    feedWeight = 0.6f, alliedFightWeight = 1.2f, trustWeight = 4f,
                    attachmentWeight = 3f, painWeight = -1f, fearWeight = -1f,
                    doctrineFamilyBias = 3f, doctrineChainedBias = -3f,
                    beastStatusBase = 0.6f, beastStatusBondWeight = 0.4f,
                    behaviour = new HoundBehaviourParams
                    {
                        aggressionMultiplier = 1f, bellResponseMultiplier = 1f,
                        villagerComfortRadius = 4f
                    }
                },
                new HoundPathProfile
                {
                    path = HoundPath.War,
                    feedWeight = 0.5f, alliedFightWeight = 1.6f, soloHuntWeight = 0.8f,
                    trustWeight = 1f, fearWeight = 1.5f, painWeight = 0.5f,
                    doctrineWeaponBias = 4f, doctrineFamilyBias = -1f,
                    beastStatusBase = -0.2f, beastStatusBondWeight = 0.25f,
                    behaviour = new HoundBehaviourParams
                    {
                        aggressionMultiplier = 1.6f, bellResponseMultiplier = 1f,
                        villagerComfortRadius = 0f
                    }
                },
                new HoundPathProfile
                {
                    path = HoundPath.Starved,
                    soloHuntWeight = 1.6f, hungerWeight = 5f, injuryWeight = 0.3f,
                    trustWeight = -1.5f, feedWeight = -1.2f,
                    doctrineChainedBias = 1f, doctrineFamilyBias = -2f,
                    beastStatusBase = -0.4f, beastStatusBondWeight = 0.2f,
                    behaviour = new HoundBehaviourParams
                    {
                        aggressionMultiplier = 1.1f, bellResponseMultiplier = 0f,
                        huntsAlone = true
                    }
                },
                new HoundPathProfile
                {
                    path = HoundPath.Sacred,
                    riteWeight = 2f, trustWeight = 1.5f, attachmentWeight = 1.5f,
                    fearWeight = -1f, painWeight = -0.5f,
                    doctrineSacredBias = 5f, doctrineWeaponBias = -3f,
                    beastStatusBase = 0.5f, beastStatusBondWeight = 0.3f,
                    behaviour = new HoundBehaviourParams
                    {
                        aggressionMultiplier = 0.6f, bellResponseMultiplier = 1f,
                        fightsOnlyAtAbbey = true, villagerComfortRadius = 2f
                    }
                },
                new HoundPathProfile
                {
                    path = HoundPath.Broken,
                    chainWeight = 1.4f, painWeight = 5f, fearWeight = 4f, injuryWeight = 1.2f,
                    trustWeight = -2f,
                    doctrineChainedBias = 4f, doctrineFamilyBias = -2f,
                    beastStatusBase = -0.6f, beastStatusBondWeight = 0.25f,
                    behaviour = new HoundBehaviourParams
                    {
                        aggressionMultiplier = 0.8f, bellResponseMultiplier = 0f,
                        unreliable = true
                    }
                },
            };
        }

        static HoundEvolutionConfig _cached;

        /// <summary>Resources asset if present, otherwise a coded-default instance. Never null.</summary>
        public static HoundEvolutionConfig LoadOrDefault()
        {
            if (_cached != null)
            {
                return _cached;
            }
            _cached = Resources.Load<HoundEvolutionConfig>(ResourcePath);
            if (_cached == null)
            {
                _cached = CreateInstance<HoundEvolutionConfig>();
                _cached.name = "HoundEvolutionConfig (defaults)";
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
