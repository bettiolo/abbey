using Abbey.Core;
using Abbey.Light;
using Abbey.Villagers;
using UnityEngine;

namespace Abbey.Nightmares
{
    /// <summary>
    /// Forest-misdirection layer for the Map 1 systems-test map: high Forest Debt
    /// brings fog that hides non-sacred lantern edges, and a Bell Mimic can emit a
    /// False Bell that lures villagers toward a false light. The True Bell remains
    /// the existing EventBus.BellRang path; VillagerAgent clears false guidance when
    /// a bell-boosted recall reaches it.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class FalseGuidanceSystem : MonoBehaviour
    {
        public static FalseGuidanceSystem Instance { get; private set; }

        ThreatConfig _config;
        bool _isDuplicate;

        public bool FogActive { get; private set; }
        public int FalseBellCount { get; private set; }
        public int FalseGuidanceOrders { get; private set; }
        public int PathShiftCount { get; private set; }
        public Vector3 LastFalseBellOrigin { get; private set; }
        public Vector3 LastFalseLightTarget { get; private set; }

        public ThreatConfig Config
        {
            get
            {
                if (_config == null)
                {
                    _config = ThreatConfig.LoadOrDefault();
                }
                return _config;
            }
            set { _config = value; }
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                _isDuplicate = true;
                Debug.LogWarning("[FalseGuidanceSystem] Duplicate instance ignored.", this);
                if (Application.isPlaying)
                {
                    Destroy(gameObject);
                }
                return;
            }
            _isDuplicate = false;
            Instance = this;
        }

        void OnEnable()
        {
            if (_isDuplicate)
            {
                return;
            }
            EventBus.PhaseChanged -= OnPhaseChanged;
            EventBus.PhaseChanged += OnPhaseChanged;
        }

        void OnDisable()
        {
            EventBus.PhaseChanged -= OnPhaseChanged;
            if (Instance == this)
            {
                ClearMisdirection();
            }
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        void OnPhaseChanged(DayPhase phase)
        {
            if (_isDuplicate)
            {
                return;
            }
            if (phase == DayPhase.Night)
            {
                BeginNightFromDebt();
            }
            else if (phase == DayPhase.Dawn)
            {
                ClearMisdirection();
            }
        }

        /// <summary>Starts fog/path-shift pressure when Forest Debt crosses the data threshold.</summary>
        public bool BeginNightFromDebt()
        {
            var threat = ThreatSourceSystem.Instance;
            float debt = threat != null ? threat.PressureFor(ThreatSourceType.Forest) : 0f;
            if (debt < Config.forestMisdirectionDebtThreshold)
            {
                return false;
            }
            ApplyMisdirectionFog($"forest_debt={debt:F2}");
            return true;
        }

        /// <summary>Called by the director when a misdirection nightmare enters the map.</summary>
        public void RecordNightmareSpawn(NightmareType type, Vector3 position, Vector3 villageCenter)
        {
            switch (type)
            {
                case NightmareType.RootWalker:
                case NightmareType.AntlerWraith:
                case NightmareType.HollowDeer:
                case NightmareType.CharcoalDead:
                    ApplyMisdirectionFog($"spawn={type}");
                    break;
                case NightmareType.BellMimic:
                    ApplyMisdirectionFog("spawn=BellMimic");
                    EmitFalseBell(position, villageCenter, "bell_mimic");
                    break;
            }
        }

        /// <summary>
        /// Emits a False Bell: villagers inside the mimic radius follow a projected
        /// false light instead of the nearest real safe light. Returns affected count.
        /// </summary>
        public int EmitFalseBell(Vector3 origin, Vector3 villageCenter, string reason)
        {
            var cfg = Config;
            Vector3 dir = PlanarMotion.Direction(villageCenter, origin);
            Vector3 target = origin + dir * cfg.falseLightDistance;
            LastFalseBellOrigin = origin;
            LastFalseLightTarget = target;
            FalseBellCount++;

            int affected = 0;
            var villagers = DuskRecallSystem.Villagers;
            for (int i = 0; i < villagers.Count; i++)
            {
                var v = villagers[i];
                if (v == null)
                {
                    continue;
                }
                if (PlanarMotion.Distance(v.transform.position, origin) > cfg.falseBellRadius)
                {
                    continue;
                }
                v.FollowFalseGuidance(target, cfg.falseGuidanceFear);
                affected++;
            }

            FalseGuidanceOrders += affected;
            PathShiftCount += affected > 0 ? 1 : 0;
            GameEventLog.Append("false_bell",
                $"reason={reason} origin=({origin.x:F1},{origin.z:F1}) " +
                $"target=({target.x:F1},{target.z:F1}) villagers={affected}");
            return affected;
        }

        public void ClearMisdirection()
        {
            FogActive = false;
            DarknessEvaluator.MisdirectionLightMultiplier = 1f;
        }

        void ApplyMisdirectionFog(string reason)
        {
            FogActive = true;
            DarknessEvaluator.MisdirectionLightMultiplier = Config.misdirectionLanternMultiplier;
            GameEventLog.Append("misdirection",
                $"fog_active reason={reason} lantern_multiplier={Config.misdirectionLanternMultiplier:F2}");
        }
    }
}
