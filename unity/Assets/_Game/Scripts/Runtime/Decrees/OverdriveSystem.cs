using System.Collections.Generic;
using Abbey.Core;
using Abbey.Economy;
using Abbey.Light;
using Abbey.Sanity;
using Abbey.Villagers;
using UnityEngine;

namespace Abbey.Decrees
{
    /// <summary>
    /// The emergency overdrive system (P3-08): the player's panic buttons. Each lever
    /// (<see cref="OverdriveActionId"/>) buys immediate night capability and books a
    /// cost — some paid now (resources debited from the <see cref="ResourceLedger"/>, a
    /// sanity hit on each participant, a trust / beast-status delta), some deferred (a
    /// dread the <see cref="SanitySystem"/> adds to each participant at dawn, and
    /// nightmare-debt points the <c>NightmareDirector</c> cashes in on later nights).
    ///
    /// Per-night lifecycle: <see cref="Activate"/> pays the immediate costs, drives the
    /// effect (candle carriers as mobile lights, lantern overburn, a bell toll…), and
    /// keeps participating villagers out of the dusk recall; <see cref="Tick"/> meters the
    /// per-hour upkeep (a lever that runs out of candles/oil stands down with a risk tag);
    /// <see cref="SettleAtDawn"/> hands the villagers back, restores the lights and adds
    /// each lever's deferred dread + nightmare debt. Every activation and cost is
    /// event-logged. Deterministic (no RNG). All balance lives in
    /// <see cref="OverdriveConfig"/>. Singleton + [ExecuteAlways] like the other Phase 3
    /// systems so EditMode tests get the OnEnable/OnDisable lifecycle. The dawn settle
    /// runs off <see cref="EventBus.PhaseChanged"/>; tests call the methods directly.
    ///
    /// P3-09 night-labour laws will gate which levers are permitted — set
    /// <see cref="PermissionProvider"/> (default null ⇒ everything allowed). P3-11
    /// nightmares consume <see cref="PendingNightmareDebt"/> via
    /// <see cref="ConsumeNightmareDebtForNight"/>.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class OverdriveSystem : MonoBehaviour
    {
        public static OverdriveSystem Instance { get; private set; }

        [Tooltip("Advance upkeep from Update using Time.deltaTime. Tests set false and call Tick().")]
        public bool autoTick = true;

        [Tooltip("Default candle-line route length (from this transform, +Z) when no context is given.")]
        [Min(0f)] public float candleRouteLength = 12f;

        /// <summary>
        /// The night-labour permission gate (P3-09 writes it). Null ⇒ every lever is
        /// permitted. Returning false refuses the activation before any cost is paid.
        /// </summary>
        public System.Func<OverdriveActionId, bool> PermissionProvider { get; set; }

        OverdriveConfig _config;
        bool _isDuplicate;

        readonly List<OverdriveAction> _active = new List<OverdriveAction>();
        readonly Dictionary<OverdriveActionId, int> _lastActivatedDay =
            new Dictionary<OverdriveActionId, int>();
        readonly List<LightSource> _lightScratch = new List<LightSource>();

        /// <summary>Every lever fired tonight (active or stood-down). Debug / downstream read.</summary>
        public IReadOnlyList<OverdriveAction> ActiveActions => _active;

        /// <summary>Accumulated nightmare debt not yet spent by a night (P3-11 consumes it).</summary>
        public float PendingNightmareDebt { get; private set; }

        /// <summary>Nightmare-debt points fired tonight that will settle into the pool at dawn.</summary>
        public float TonightDebtAccrual
        {
            get
            {
                float sum = 0f;
                for (int i = 0; i < _active.Count; i++)
                {
                    sum += _active[i].NightmareDebtPoints;
                }
                return sum;
            }
        }

        public OverdriveConfig Config
        {
            get
            {
                if (_config == null)
                {
                    _config = OverdriveConfig.LoadOrDefault();
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
                Debug.LogWarning("[OverdriveSystem] Duplicate instance ignored.", this);
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
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>Injects a config (tests) and clears all live/pending state.</summary>
        public void Configure(OverdriveConfig config)
        {
            _config = config;
            StandDownAll();
            _active.Clear();
            _lastActivatedDay.Clear();
            PendingNightmareDebt = 0f;
        }

        void Update()
        {
            if (_isDuplicate || !Application.isPlaying || !autoTick)
            {
                return;
            }
            Tick(Time.deltaTime);
        }

        void OnPhaseChanged(DayPhase phase)
        {
            if (_isDuplicate)
            {
                return;
            }
            if (phase == DayPhase.Dawn)
            {
                SettleAtDawn();
            }
        }

        // ------------------------------------------------------------------
        // Permission gate (P3-09 laws)
        // ------------------------------------------------------------------

        /// <summary>Whether a lever may be fired (default allowed; P3-09 laws override).</summary>
        public bool IsPermitted(OverdriveActionId id)
        {
            return PermissionProvider == null || PermissionProvider(id);
        }

        // ------------------------------------------------------------------
        // Activation
        // ------------------------------------------------------------------

        /// <summary>Fires a lever using a default context (route out from this transform, all lanterns).</summary>
        public bool Activate(OverdriveActionId id)
        {
            return Activate(id, DefaultContext());
        }

        /// <summary>
        /// Fires an overdrive lever for tonight: refuses (logged) when not permitted,
        /// still on cooldown, already active tonight or unaffordable; otherwise pays the
        /// immediate costs, drives the effect and returns true. Deterministic.
        /// </summary>
        public bool Activate(OverdriveActionId id, in OverdriveContext ctx)
        {
            if (_isDuplicate)
            {
                return false;
            }
            var def = Config.DefFor(id);
            int day = GameClock.Instance != null ? GameClock.Instance.DayNumber : 1;

            if (!IsPermitted(id))
            {
                GameEventLog.Append("overdrive_refused", $"id={id} reason=not_permitted");
                return false;
            }
            if (IsActive(id))
            {
                GameEventLog.Append("overdrive_refused", $"id={id} reason=already_active");
                return false;
            }
            if (def.cooldownNights > 0
                && _lastActivatedDay.TryGetValue(id, out int last)
                && day - last < def.cooldownNights)
            {
                GameEventLog.Append("overdrive_refused",
                    $"id={id} reason=cooldown day={day} last={last}");
                return false;
            }
            if (def.immediateCost != null && !ResourceLedger.CanAfford(def.immediateCost))
            {
                GameEventLog.Append("overdrive_refused", $"id={id} reason=resources");
                return false;
            }

            // ---- Pay the immediate costs -------------------------------------
            if (def.immediateCost != null && def.immediateCost.Count > 0)
            {
                ResourceLedger.TryConsume(def.immediateCost, $"overdrive:{id}");
            }
            TrustLedger.Add(def.trustDelta, $"overdrive:{id}");
            BeastStatusLedger.Add(def.beastStatusDelta, $"overdrive:{id}");

            var action = new OverdriveAction(id, day, def);
            CollectParticipants(action.Participants);
            ApplyImmediateSanity(action, def);
            if (def.exemptsFromRecall)
            {
                for (int i = 0; i < action.Participants.Count; i++)
                {
                    action.Participants[i].NightWorkExempt = true;
                }
            }

            // ---- Drive the effect --------------------------------------------
            SpawnCandleLine(action, def, ctx);
            ApplyOverburn(action, def, ctx);
            if (def.bellRadius > 0f)
            {
                EventBus.RaiseBellRang(transform.position, def.bellRadius);
            }

            _active.Add(action);
            _lastActivatedDay[id] = day;
            GameEventLog.Append("overdrive_activated",
                $"id={id} day={day} participants={action.Participants.Count} " +
                $"candles={action.CandleLights.Count} overburn={action.OverburnedLanterns.Count} " +
                $"debt={def.nightmareDebtPoints:F1}");
            return true;
        }

        bool IsActive(OverdriveActionId id)
        {
            for (int i = 0; i < _active.Count; i++)
            {
                if (_active[i].Id == id && _active[i].Active)
                {
                    return true;
                }
            }
            return false;
        }

        OverdriveContext DefaultContext()
        {
            Vector3 start = transform.position;
            Vector3 end = start + transform.forward * Mathf.Max(0f, candleRouteLength);
            return new OverdriveContext(start, end, null);
        }

        void CollectParticipants(List<VillagerAgent> buffer)
        {
            buffer.Clear();
            var villagers = DuskRecallSystem.Villagers;
            for (int i = 0; i < villagers.Count; i++)
            {
                var v = villagers[i];
                if (v == null || v.State == VillagerState.Dead || v.State == VillagerState.Missing)
                {
                    continue;
                }
                buffer.Add(v);
            }
        }

        void ApplyImmediateSanity(OverdriveAction action, OverdriveActionDef def)
        {
            if (def.sanityCostPerVillager <= 0f)
            {
                return;
            }
            var sanity = SanitySystem.Instance;
            if (sanity == null)
            {
                return;
            }
            for (int i = 0; i < action.Participants.Count; i++)
            {
                sanity.ApplySanityCost(action.Participants[i], def.sanityCostPerVillager,
                    $"overdrive:{action.Id}");
            }
        }

        void SpawnCandleLine(OverdriveAction action, OverdriveActionDef def, in OverdriveContext ctx)
        {
            int n = Mathf.Max(0, def.candleCarriers);
            for (int i = 0; i < n; i++)
            {
                float t = n <= 1 ? 0.5f : i / (float)(n - 1);
                Vector3 pos = Vector3.Lerp(ctx.RouteStart, ctx.RouteEnd, t);
                var go = new GameObject($"CandleCarrier_{action.Id}_{i}");
                go.transform.SetParent(transform, true);
                go.transform.position = pos;
                var light = go.AddComponent<LightSource>();
                // The shared candle stock is metered by the upkeep drain, not per-light
                // fuel, so carriers hold an infinite local flame and just light the route.
                light.autoTick = false;
                light.radius = Mathf.Max(0f, def.carrierLightRadius);
                light.strength = Mathf.Clamp01(def.carrierLightStrength);
                light.fuelSeconds = -1f;
                action.CandleLights.Add(light);
            }
        }

        void ApplyOverburn(OverdriveAction action, OverdriveActionDef def, in OverdriveContext ctx)
        {
            if (def.overburnRadiusMultiplier <= 1f && def.overburnFuelMultiplier <= 1f)
            {
                return;
            }
            IReadOnlyList<LightSource> lanterns = ctx.Lanterns;
            if (lanterns == null)
            {
                CollectNonSacredLights(action, _lightScratch);
                lanterns = _lightScratch;
            }
            for (int i = 0; i < lanterns.Count; i++)
            {
                var l = lanterns[i];
                if (l == null || l.sacred)
                {
                    continue;
                }
                l.ApplyOverburn(def.overburnRadiusMultiplier, def.overburnFuelMultiplier);
                action.OverburnedLanterns.Add(l);
            }
        }

        void CollectNonSacredLights(OverdriveAction action, List<LightSource> buffer)
        {
            buffer.Clear();
            var sources = DarknessEvaluator.Sources;
            for (int i = 0; i < sources.Count; i++)
            {
                var s = sources[i];
                if (s == null || s.sacred || action.CandleLights.Contains(s))
                {
                    continue;
                }
                buffer.Add(s);
            }
        }

        // ------------------------------------------------------------------
        // Upkeep (per-hour resource drain while active)
        // ------------------------------------------------------------------

        /// <summary>Deterministic upkeep step (autoTick = false in tests).</summary>
        public void Tick(float dt)
        {
            if (_isDuplicate || dt <= 0f)
            {
                return;
            }
            for (int i = 0; i < _active.Count; i++)
            {
                var action = _active[i];
                if (!action.Active)
                {
                    continue;
                }
                var def = action.Def;
                if (def == null || def.upkeepAmount <= 0)
                {
                    continue;
                }
                action.UpkeepTimer += dt;
                float interval = Mathf.Max(0.01f, def.upkeepIntervalSeconds);
                while (action.UpkeepTimer >= interval && action.Active)
                {
                    action.UpkeepTimer -= interval;
                    if (!ResourceLedger.TryConsume(def.upkeepType, def.upkeepAmount,
                            $"overdrive:{action.Id}"))
                    {
                        GameEventLog.Append("overdrive_risk",
                            $"id={action.Id} reason=out_of_{ResourceTypes.Id(def.upkeepType)}");
                        StandDownEffects(action);
                    }
                }
            }
        }

        // ------------------------------------------------------------------
        // Stand-down + dawn settlement
        // ------------------------------------------------------------------

        /// <summary>
        /// Restores a lever's live effects — despawns its candle carriers, restores the
        /// lanterns it overburned and hands its villagers back to the recall — without
        /// touching its deferred toll (that settles at dawn). Idempotent.
        /// </summary>
        void StandDownEffects(OverdriveAction action)
        {
            if (!action.Active)
            {
                return;
            }
            action.Active = false;

            for (int i = 0; i < action.CandleLights.Count; i++)
            {
                var l = action.CandleLights[i];
                if (l == null)
                {
                    continue;
                }
                if (Application.isPlaying)
                {
                    Destroy(l.gameObject);
                }
                else
                {
                    DestroyImmediate(l.gameObject);
                }
            }
            action.CandleLights.Clear();

            for (int i = 0; i < action.OverburnedLanterns.Count; i++)
            {
                var l = action.OverburnedLanterns[i];
                if (l != null)
                {
                    l.ClearOverburn();
                }
            }
            action.OverburnedLanterns.Clear();

            for (int i = 0; i < action.Participants.Count; i++)
            {
                var v = action.Participants[i];
                if (v != null)
                {
                    v.NightWorkExempt = false;
                }
            }
            GameEventLog.Append("overdrive_stood_down", $"id={action.Id}");
        }

        void StandDownAll()
        {
            for (int i = 0; i < _active.Count; i++)
            {
                StandDownEffects(_active[i]);
            }
        }

        /// <summary>
        /// Dawn: every lever fired tonight settles its deferred toll — the sanity system
        /// adds its dread to each participant, its nightmare-debt points fall into the
        /// pending pool (spent by later nights) — then its live effects stand down and the
        /// night's slate is cleared. Public so tests can force it.
        /// </summary>
        public void SettleAtDawn()
        {
            if (_isDuplicate || _active.Count == 0)
            {
                return;
            }
            var sanity = SanitySystem.Instance;
            float settledDebt = 0f;

            for (int i = 0; i < _active.Count; i++)
            {
                var action = _active[i];
                var def = action.Def;
                if (def != null && def.deferredDreadPerVillager > 0f && sanity != null)
                {
                    for (int j = 0; j < action.Participants.Count; j++)
                    {
                        sanity.AddDread(action.Participants[j], def.deferredDreadPerVillager,
                            $"overdrive_debt:{action.Id}");
                    }
                }
                settledDebt += action.NightmareDebtPoints;
                StandDownEffects(action);
            }

            PendingNightmareDebt += settledDebt;
            GameEventLog.Append("overdrive_settled",
                $"levers={_active.Count} debt=+{settledDebt:F1} pending={PendingNightmareDebt:F1}");
            _active.Clear();
        }

        // ------------------------------------------------------------------
        // Nightmare debt (consumed by the director on later nights)
        // ------------------------------------------------------------------

        /// <summary>
        /// The director's cash-in: how many extra monsters the accumulated debt buys
        /// tonight, draining the pool by the configured fraction. Deterministic; safe to
        /// call with no debt (returns 0). P3-11 nightmares reuse this hook.
        /// </summary>
        public int ConsumeNightmareDebtForNight()
        {
            if (_isDuplicate || PendingNightmareDebt <= 0f)
            {
                return 0;
            }
            int extra = Config.DebtMonsters(PendingNightmareDebt, out float consumed);
            PendingNightmareDebt = Mathf.Max(0f, PendingNightmareDebt - consumed);
            GameEventLog.Append("overdrive_debt",
                $"consumed monsters={extra} drained={consumed:F1} pending={PendingNightmareDebt:F1}");
            return extra;
        }
    }
}
