using Abbey.Core;
using Abbey.Hero;
using Abbey.Session;
using UnityEngine;

namespace Abbey.Map2
{
    public enum BellkeeperTrait
    {
        None,
        CalmingPresence,
        CommandingVoice,
        RitualAuthority,
        HardLessons,
    }

    /// <summary>
    /// Loads the Map-1 outcome and turns it into exactly one Map-2 trait.  It does not
    /// copy the hound across maps: only what the Bellkeeper learned is carried.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class CampaignCarryoverSystem : MonoBehaviour
    {
        public static CampaignCarryoverSystem Instance { get; private set; }

        Map2Config _config;
        bool _isDuplicate;

        public CampaignOutcome Outcome { get; private set; }
        public BellkeeperTrait Trait { get; private set; }

        public float StagTrustBonus => Trait == BellkeeperTrait.CalmingPresence
            ? Config.calmingPresenceTrustBonus : 0f;
        public float StagCovenantBonus => Trait == BellkeeperTrait.RitualAuthority
            ? Config.ritualAuthorityCovenantBonus : 0f;
        public float BellRadiusMultiplier => Trait == BellkeeperTrait.CommandingVoice
            ? Config.commandingVoiceBellRadiusMultiplier : 1f;
        public float FalseGuidanceFearMultiplier => Trait == BellkeeperTrait.CalmingPresence
            ? Config.calmingPresenceFalseBellFearMultiplier : 1f;
        public float ForestDebtGrace => Trait == BellkeeperTrait.HardLessons
            ? Config.hardLessonsDebtGrace : 0f;

        public Map2Config Config
        {
            get => _config != null ? _config : (_config = Map2Config.LoadOrDefault());
            set => _config = value;
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                _isDuplicate = true;
                if (Application.isPlaying) Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void Start()
        {
            if (_isDuplicate) return;
            if (Application.isPlaying && Outcome == null)
            {
                Configure(CampaignOutcome.Load());
            }
            ApplyToWorld();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Configure(CampaignOutcome outcome, Map2Config config = null)
        {
            if (config != null) _config = config;
            Outcome = outcome;
            Trait = DeriveTrait(outcome);
            GameEventLog.Append("campaign_carryover",
                $"trait={Trait} source={(outcome != null ? outcome.result : "none")}");
            ApplyToWorld();
        }

        public void ApplyToWorld()
        {
            var hero = FindFirstObjectByType<BellkeeperController>();
            if (hero != null) hero.BellRadiusTraitMultiplier = BellRadiusMultiplier;
            var stag = FindFirstObjectByType<StagCovenantSystem>();
            if (stag != null) stag.RecomputeFromLog();
        }

        public static BellkeeperTrait DeriveTrait(CampaignOutcome outcome)
        {
            if (outcome == null || outcome.Result != CampaignResult.ShipSailed)
            {
                return BellkeeperTrait.None;
            }

            if (outcome.houndPath == "Broken" || outcome.houndPath == "Starved"
                || outcome.abbeyForm == "Broken" || outcome.villagerDeaths >= 4)
            {
                return BellkeeperTrait.HardLessons;
            }
            if (outcome.houndPath == "War" || outcome.abbeyForm == "Fortress")
            {
                return BellkeeperTrait.CommandingVoice;
            }
            if (outcome.houndPath == "Sacred" || outcome.abbeyForm == "Sanctuary"
                || outcome.abbeyForm == "Cult")
            {
                return BellkeeperTrait.RitualAuthority;
            }
            return BellkeeperTrait.CalmingPresence;
        }
    }
}
