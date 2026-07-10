using System;
using Abbey.Combat;
using Abbey.Core;
using Abbey.Light;
using Abbey.Rendering;
using Abbey.Villagers;
using Abbey.World;
using UnityEngine;

namespace Abbey.Nightmares
{
    /// <summary>
    /// The runtime marker for tonight's dark objective (P3-06). It sits at a location
    /// that classified Dark at generation time (outside every Safe light), so only a
    /// dark-capable unit — a <see cref="WarriorAgent"/>, the hero or the beast — can
    /// reach and clear it. A watchtower's vision "arms" (reveals) it; with no
    /// watchtower in the settlement it is revealed by default so the mechanic still
    /// runs. Solving it (a warrior arriving) fires <see cref="NightEscalationSystem.DarkObjectiveSolved"/>;
    /// leaving it unsolved at dawn applies its consequence. [ExecuteAlways] for the
    /// EditMode lifecycle.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class DarkObjectiveMarker : MonoBehaviour
    {
        public DarkObjective Objective { get; private set; }

        public bool IsSolved { get; private set; }

        /// <summary>Which warrior cleared it (null until solved).</summary>
        public WarriorAgent SolvedBy { get; private set; }

        NightEscalationSystem _owner;
        CombatConfig _combat;

        /// <summary>Binds the marker to its data + owning system.</summary>
        public void Configure(DarkObjective objective, NightEscalationSystem owner, CombatConfig combat)
        {
            Objective = objective;
            _owner = owner;
            _combat = combat;
            transform.position = objective.Location;
            IsSolved = false;
            SolvedBy = null;
        }

        /// <summary>
        /// True when a watchtower's vision covers this marker — or when the settlement
        /// has no watchtower at all (the objective cannot be gated out of existence by
        /// a missing optional building).
        /// </summary>
        public bool IsRevealed
        {
            get
            {
                var towers = WarriorStructure.Active;
                bool anyTower = false;
                for (int i = 0; i < towers.Count; i++)
                {
                    var t = towers[i];
                    if (t == null || t.Role != WarriorStructureRole.Watchtower)
                    {
                        continue;
                    }
                    anyTower = true;
                    float vision = (_combat != null ? _combat : CombatConfig.LoadOrDefault())
                        .watchtowerVisionRadius;
                    if (PlanarMotion.Distance(t.transform.position, transform.position) <= vision)
                    {
                        return true;
                    }
                }
                return !anyTower;
            }
        }

        /// <summary>A dark-capable unit reached the marker and cleared it. Idempotent.</summary>
        public void Solve(WarriorAgent by)
        {
            if (IsSolved)
            {
                return;
            }
            IsSolved = true;
            SolvedBy = by;
            GameEventLog.Append("dark_objective",
                $"solved kind={Objective.KindId} by={(by != null ? by.name : "unknown")}");
            _owner?.NotifySolved(this);
        }
    }

    /// <summary>
    /// The Phase 3 night-escalation system (P3-06). Two jobs, both data-driven from the
    /// <see cref="CombatConfig"/> escalation section (AGENTS.md: curve data in config,
    /// not code):
    ///
    /// <list type="number">
    /// <item><b>Wave budget curve.</b> <see cref="WaveMonsterCount"/> turns a night
    /// index + <see cref="Season"/> into how many monsters the
    /// <see cref="NightmareDirector"/> spawns — a monotonic ramp within a season that
    /// steps up in Autumn/Winter, with periodic set-piece stands
    /// (<see cref="IsSetPieceNight"/>) that spike the wave.</item>
    /// <item><b>Nightly dark objective.</b> <see cref="BeginNight"/> generates one
    /// deterministic objective (<see cref="DarkObjectiveGenerator"/>) out in the Dark
    /// and spawns its <see cref="DarkObjectiveMarker"/>; <see cref="ResolveNight"/> at
    /// dawn clears it — no penalty if a warrior solved it, its configured consequence
    /// (a villager lost to the dark, a lantern left breached, a nest left burning)
    /// otherwise.</item>
    /// </list>
    ///
    /// Deterministic (seeded from config + night index). Singleton like the other
    /// Phase 3 systems. [ExecuteAlways] for the EditMode lifecycle.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class NightEscalationSystem : MonoBehaviour
    {
        public static NightEscalationSystem Instance { get; private set; }

        /// <summary>A warrior (or the hero) cleared tonight's dark objective.</summary>
        public static event Action<DarkObjective> DarkObjectiveSolved;

        /// <summary>Tonight's dark objective went unanswered — its consequence lands.</summary>
        public static event Action<DarkObjective> DarkObjectiveFailed;

        /// <summary>Test isolation for the static hooks.</summary>
        public static void ResetStaticEvents()
        {
            DarkObjectiveSolved = null;
            DarkObjectiveFailed = null;
        }

        CombatConfig _combat;
        PrototypeConfig _proto;
        bool _isDuplicate;
        DarkObjectiveMarker _marker;

        public CombatConfig Combat
        {
            get
            {
                if (_combat == null)
                {
                    _combat = CombatConfig.LoadOrDefault();
                }
                return _combat;
            }
            set { _combat = value; }
        }

        public PrototypeConfig Config
        {
            get
            {
                if (_proto == null)
                {
                    _proto = PrototypeConfig.LoadOrDefault();
                }
                return _proto;
            }
            set { _proto = value; }
        }

        // ---- Debug-panel state (display only) ----------------------------

        public float TonightWaveBudget { get; private set; }
        public int TonightMonsterCount { get; private set; }
        public bool IsSetPieceTonight { get; private set; }
        public int NightIndex { get; private set; }
        public Season NightSeason { get; private set; }

        /// <summary>Tonight's dark-objective marker, or null between nights.</summary>
        public DarkObjectiveMarker ActiveMarker => _marker;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                _isDuplicate = true;
                Debug.LogWarning("[NightEscalationSystem] Duplicate instance ignored.", this);
                if (Application.isPlaying)
                {
                    Destroy(gameObject);
                }
                return;
            }
            _isDuplicate = false;
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        // ------------------------------------------------------------------
        // Escalation curve (pure, static so EditMode tests call it directly)
        // ------------------------------------------------------------------

        /// <summary>
        /// The monotonic base wave budget for a night: (base + growth·(n-1)) scaled by
        /// the season multiplier. Non-decreasing in night index within a season and
        /// larger in Autumn/Winter. Excludes the set-piece spike (see
        /// <see cref="FinalWaveBudget"/>).
        /// </summary>
        public static float WaveBudget(CombatConfig cfg, int nightIndex, Season season)
        {
            if (cfg == null)
            {
                cfg = CombatConfig.LoadOrDefault();
            }
            float n = Mathf.Max(1, nightIndex);
            float baseBudget = cfg.escalationBaseWaveBudget
                               + cfg.escalationPerNightGrowth * (n - 1f);
            return Mathf.Max(0f, baseBudget) * cfg.SeasonWaveMultiplier(season);
        }

        /// <summary>True on a set-piece stand night (every Nth night; 0 disables).</summary>
        public static bool IsSetPieceNight(CombatConfig cfg, int nightIndex)
        {
            if (cfg == null)
            {
                cfg = CombatConfig.LoadOrDefault();
            }
            return cfg.escalationSetPieceEveryNNights > 0 && nightIndex > 0
                   && nightIndex % cfg.escalationSetPieceEveryNNights == 0;
        }

        /// <summary>The base budget with the set-piece spike applied on stand nights.</summary>
        public static float FinalWaveBudget(CombatConfig cfg, int nightIndex, Season season)
        {
            if (cfg == null)
            {
                cfg = CombatConfig.LoadOrDefault();
            }
            float budget = WaveBudget(cfg, nightIndex, season);
            return IsSetPieceNight(cfg, nightIndex)
                ? budget * Mathf.Max(1f, cfg.escalationSetPieceMultiplier)
                : budget;
        }

        /// <summary>Monsters to spawn tonight: final budget / unit cost, clamped to the cap.</summary>
        public static int WaveMonsterCount(CombatConfig cfg, int nightIndex, Season season)
        {
            if (cfg == null)
            {
                cfg = CombatConfig.LoadOrDefault();
            }
            float budget = FinalWaveBudget(cfg, nightIndex, season);
            int count = Mathf.CeilToInt(budget / Mathf.Max(0.01f, cfg.escalationMonsterUnitCost));
            return Mathf.Clamp(count, 0, Mathf.Max(0, cfg.escalationMaxWaveMonsters));
        }

        // ------------------------------------------------------------------
        // Night lifecycle (driven by the NightmareDirector)
        // ------------------------------------------------------------------

        /// <summary>
        /// Opens a Phase 3 night: latches tonight's wave budget/count/set-piece flag and
        /// generates + spawns the dark-objective marker (deterministic from the config
        /// seed + night index). Returns the monster count the director should spawn.
        /// </summary>
        public int BeginNight(int nightIndex, Season season, Vector3 center)
        {
            if (_isDuplicate)
            {
                return 0;
            }
            var cfg = Combat;
            NightIndex = nightIndex;
            NightSeason = season;
            TonightWaveBudget = FinalWaveBudget(cfg, nightIndex, season);
            IsSetPieceTonight = IsSetPieceNight(cfg, nightIndex);
            TonightMonsterCount = WaveMonsterCount(cfg, nightIndex, season);

            GameEventLog.Append("night_escalation",
                $"night={nightIndex} season={season} budget={TonightWaveBudget:F1} " +
                $"monsters={TonightMonsterCount} setPiece={IsSetPieceTonight}");

            ClearMarker();
            var proto = Config;
            if (DarkObjectiveGenerator.TryGenerate(proto.simulationSeed, nightIndex, center,
                    proto.monsterSpawnMinRadius, proto.monsterSpawnMaxRadius,
                    proto.monsterSpawnAttempts, out var objective))
            {
                SpawnObjective(objective);
            }
            else
            {
                GameEventLog.Append("dark_objective",
                    $"skipped night={nightIndex} reason=no_dark_point");
            }

            return TonightMonsterCount;
        }

        /// <summary>Spawns a marker for the given objective and makes it the active one. Public for tests.</summary>
        public DarkObjectiveMarker SpawnObjective(DarkObjective objective)
        {
            ClearMarker();
            var go = new GameObject("DarkObjective");
            go.transform.position = objective.Location;
            _marker = go.AddComponent<DarkObjectiveMarker>();
            _marker.Configure(objective, this, Combat);
            SpriteProjectionBootstrap.RegisterGlobal(
                go, "night_objective", "dynamic.nightObjective", $"objective:{objective.KindId}");
            GameEventLog.Append("dark_objective",
                $"spawn kind={objective.KindId} pos=({objective.Location.x:F1},{objective.Location.z:F1})");
            return _marker;
        }

        /// <summary>Solve callback from the marker: raises the static event.</summary>
        internal void NotifySolved(DarkObjectiveMarker marker)
        {
            if (marker != null)
            {
                DarkObjectiveSolved?.Invoke(marker.Objective);
            }
        }

        /// <summary>
        /// Closes a Phase 3 night: an unsolved dark objective applies its consequence
        /// (a villager lost, a lantern left breached, a nest left burning) and fires
        /// <see cref="DarkObjectiveFailed"/>; then the marker is despawned.
        /// </summary>
        public void ResolveNight()
        {
            if (_isDuplicate || _marker == null)
            {
                return;
            }
            if (!_marker.IsSolved)
            {
                ApplyConsequence(_marker.Objective);
                DarkObjectiveFailed?.Invoke(_marker.Objective);
            }
            ClearMarker();
        }

        void ApplyConsequence(DarkObjective objective)
        {
            switch (objective.Kind)
            {
                case DarkObjectiveKind.DownedVillager:
                    var lost = NearestExposedVillager(objective.Location);
                    if (lost != null)
                    {
                        lost.ForceState(VillagerState.Missing);
                        GameEventLog.Append("dark_objective",
                            $"failed kind={objective.KindId} consequence=villager_lost villager={lost.name}");
                        return;
                    }
                    GameEventLog.Append("dark_objective",
                        $"failed kind={objective.KindId} consequence=villager_lost villager=none");
                    break;

                case DarkObjectiveKind.BreachedLantern:
                    GameEventLog.Append("dark_objective",
                        $"failed kind={objective.KindId} consequence=lantern_stays_dark");
                    break;

                default:
                    GameEventLog.Append("dark_objective",
                        $"failed kind={objective.KindId} consequence=nest_ignites");
                    break;
            }
        }

        static VillagerAgent NearestExposedVillager(Vector3 position)
        {
            VillagerAgent best = null;
            float bestDist = float.MaxValue;
            var villagers = DuskRecallSystem.Villagers;
            for (int i = 0; i < villagers.Count; i++)
            {
                var v = villagers[i];
                if (v == null || v.State == VillagerState.Dead || v.State == VillagerState.Missing)
                {
                    continue;
                }
                if (DarknessEvaluator.Classify(v.transform.position) == LightZone.Safe)
                {
                    continue; // sheltered villagers are safe; the loss is out in the dark
                }
                float dist = PlanarMotion.Distance(position, v.transform.position);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = v;
                }
            }
            return best;
        }

        void ClearMarker()
        {
            if (_marker == null)
            {
                return;
            }
            var go = _marker.gameObject;
            _marker = null;
            if (Application.isPlaying)
            {
                Destroy(go);
            }
            else
            {
                DestroyImmediate(go);
            }
        }
    }
}
