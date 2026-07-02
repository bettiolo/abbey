using Abbey.Beast;
using Abbey.CameraRig;
using Abbey.Core;
using Abbey.Light;
using Abbey.Villagers;
using UnityEngine;

namespace Abbey.Hero
{
    /// <summary>
    /// The directly controlled Bellkeeper: rescuer, signaler, guardian — not a
    /// generic warrior. Movement is kinematic XZ steering: legacy Input axes when
    /// <see cref="useDirectInput"/> is on, or the SetMoveTarget/MoveTowards API that
    /// tests and click-to-move drive. Abilities (RingBell, CarryFlame, Rescue,
    /// FeedHound) all pull ranges, costs and cooldowns from
    /// <see cref="PrototypeConfig"/>. Health and stamina stay simple.
    /// [ExecuteAlways] so EditMode tests get lifecycle; Update only ticks in play.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class BellkeeperController : MonoBehaviour
    {
        [Tooltip("Read legacy Input axes (Horizontal/Vertical) each tick in play mode.")]
        public bool useDirectInput;

        [Tooltip("Advance from Update using Time.deltaTime. Tests set false and call Tick().")]
        public bool autoTick = true;

        PrototypeConfig _config;
        bool _initialized;
        Vector3? _moveTarget;
        float _bellCooldown;
        float _rescueCooldown;
        float _feedCooldown;
        float _flameCooldown;
        GameObject _flameGO;
        LightSource _carriedFlame;
        VillagerAgent _escorting;

        public float Health { get; private set; }

        public float Stamina { get; private set; }

        public int CarriedFood { get; private set; }

        public bool IsAlive => !_initialized || Health > 0f;

        public bool IsCarryingFlame => _carriedFlame != null && _carriedFlame.isLit;

        public VillagerAgent EscortedVillager => _escorting;

        public bool HasMoveTarget => _moveTarget.HasValue;

        public PrototypeConfig Config
        {
            get
            {
                if (_config == null)
                {
                    _config = PrototypeConfig.LoadOrDefault();
                }
                return _config;
            }
            set { _config = value; }
        }

        void OnEnable()
        {
            EnsureInit();
        }

        void Update()
        {
            if (!Application.isPlaying || !autoTick)
            {
                return;
            }
            Tick(Time.deltaTime);
        }

        void EnsureInit()
        {
            if (_initialized)
            {
                return;
            }
            var cfg = Config;
            Health = cfg.bellkeeperMaxHealth;
            Stamina = cfg.bellkeeperMaxStamina;
            CarriedFood = cfg.startingCarriedFood;
            _initialized = true;
        }

        /// <summary>Injects a config (tests) and resets health/stamina/food to its values.</summary>
        public void Configure(PrototypeConfig config)
        {
            _config = config;
            _initialized = false;
            EnsureInit();
        }

        /// <summary>Orders the hero to walk to a world position (arrival radius from config).</summary>
        public void SetMoveTarget(Vector3 worldPosition)
        {
            _moveTarget = worldPosition;
        }

        /// <summary>Alias of <see cref="SetMoveTarget"/> for test readability.</summary>
        public void MoveTowards(Vector3 worldPosition)
        {
            SetMoveTarget(worldPosition);
        }

        public void ClearMoveTarget()
        {
            _moveTarget = null;
        }

        /// <summary>
        /// Rings the bell: raises BellRang(position, config.bellRadius) — recall
        /// pulse for villagers and call for the hound. False while on cooldown.
        /// </summary>
        public bool RingBell()
        {
            EnsureInit();
            if (_bellCooldown > 0f || !IsAlive)
            {
                return false;
            }
            _bellCooldown = Config.bellCooldownSeconds;
            GameEventLog.Append("hero_rang_bell", $"pos={transform.position}");
            EventBus.RaiseBellRang(transform.position, Config.bellRadius);
            return true;
        }

        /// <summary>
        /// Raises or douses the carried flame — a small mobile LightSource child
        /// that drains stamina per second while lit. False on cooldown, when out of
        /// stamina, or when dead.
        /// </summary>
        public bool CarryFlame(bool lit)
        {
            EnsureInit();
            if (lit == IsCarryingFlame)
            {
                return true;
            }
            if (_flameCooldown > 0f || !IsAlive || (lit && Stamina <= 0f))
            {
                return false;
            }
            _flameCooldown = Config.carryFlameCooldownSeconds;

            if (_flameGO == null)
            {
                _flameGO = new GameObject("CarriedFlame");
                _flameGO.transform.SetParent(transform, false);
                _flameGO.transform.localPosition = Vector3.zero;
                _carriedFlame = _flameGO.AddComponent<LightSource>();
                _carriedFlame.radius = Config.carriedFlameRadius;
                _carriedFlame.strength = Config.carriedFlameStrength;
                _carriedFlame.fuelSeconds = -1f; // the cost is hero stamina, not fuel
                _carriedFlame.autoTick = false;
                _carriedFlame.isLit = false;
            }

            if (lit)
            {
                _carriedFlame.Ignite();
                GameEventLog.Append("hero_raised_flame", name);
            }
            else
            {
                _carriedFlame.Extinguish();
                GameEventLog.Append("hero_doused_flame", name);
            }
            return IsCarryingFlame == lit;
        }

        /// <summary>
        /// Attaches a villager within interactRange: it enters rescued-follow and
        /// trails the hero. Release near Safe light with <see cref="ReleaseRescued"/>.
        /// </summary>
        public bool Rescue(VillagerAgent villager)
        {
            EnsureInit();
            if (villager == null || _rescueCooldown > 0f || !IsAlive || _escorting != null)
            {
                return false;
            }
            if (PlanarMotion.Distance(transform.position, villager.transform.position)
                > Config.interactRange)
            {
                return false;
            }
            if (!villager.BeginRescue(transform))
            {
                return false;
            }
            _rescueCooldown = Config.rescueCooldownSeconds;
            _escorting = villager;
            GameEventLog.Append("hero_rescue_started", villager.name);
            return true;
        }

        /// <summary>
        /// Releases the escorted villager. In a Safe zone the rescue completes and
        /// VillagerRescued is raised; in darkness the villager walks on alone.
        /// </summary>
        public bool ReleaseRescued()
        {
            if (_escorting == null)
            {
                return false;
            }
            var villager = _escorting;
            _escorting = null;
            bool safe = villager.ReleaseRescue();
            GameEventLog.Append("hero_rescue_released", $"{villager.name} safe={safe}");
            return safe;
        }

        /// <summary>
        /// Feeds the hound one unit of carried food when within interactRange.
        /// False on cooldown, out of range, out of food, or dead.
        /// </summary>
        public bool FeedHound(HoundController hound)
        {
            EnsureInit();
            if (hound == null || _feedCooldown > 0f || CarriedFood <= 0 || !IsAlive)
            {
                return false;
            }
            if (PlanarMotion.Distance(transform.position, hound.transform.position)
                > Config.interactRange)
            {
                return false;
            }
            _feedCooldown = Config.feedCooldownSeconds;
            CarriedFood--;
            GameEventLog.Append("hero_fed_hound", $"foodLeft={CarriedFood}");
            hound.Feed();
            return true;
        }

        /// <summary>
        /// Unchains the hound when within interactRange. The outcome belongs to the
        /// bond: a no-trust hound turns Angry and flees to Missing; decent trust
        /// keeps it (Wary/Fed/Following). False out of range, already free, or dead.
        /// </summary>
        public bool FreeHound(HoundController hound)
        {
            EnsureInit();
            if (hound == null || !IsAlive || !InReachOf(hound.transform))
            {
                return false;
            }
            if (!hound.FreeFromChain(transform.position))
            {
                return false;
            }
            GameEventLog.Append("hero_freed_hound", hound.name);
            return true;
        }

        /// <summary>
        /// The high-risk calming touch: approach the hound slowly. Deterministic —
        /// a hound past its fear+pain bite threshold bites and injures the hero
        /// (houndBiteDamage); otherwise attachment grows. False when unavailable.
        /// </summary>
        public bool ApproachHound(HoundController hound)
        {
            EnsureInit();
            if (hound == null || !IsAlive || !InReachOf(hound.transform))
            {
                return false;
            }
            var result = hound.ApproachSlowly();
            if (result == HoundApproachResult.Unavailable)
            {
                return false;
            }
            if (result == HoundApproachResult.Bitten)
            {
                GameEventLog.Append("hero_bitten_by_hound", $"{name} by {hound.name}");
                TakeDamage(Config.houndBiteDamage);
            }
            return true;
        }

        /// <summary>
        /// The explicit walk-away from the chained hound. Costs nothing now; the
        /// hound remembers (resentment), and the log records the choice.
        /// </summary>
        public bool LeaveHoundChained(HoundController hound)
        {
            EnsureInit();
            if (hound == null || !IsAlive || !InReachOf(hound.transform))
            {
                return false;
            }
            if (!hound.LeaveChained())
            {
                return false;
            }
            GameEventLog.Append("hero_left_hound_chained", hound.name);
            return true;
        }

        bool InReachOf(Transform target)
        {
            return PlanarMotion.Distance(transform.position, target.position)
                   <= Config.interactRange;
        }

        public void TakeDamage(float amount)
        {
            EnsureInit();
            if (amount <= 0f || !IsAlive)
            {
                return;
            }
            Health = Mathf.Max(0f, Health - amount);
            if (Health <= 0f)
            {
                GameEventLog.Append("hero_died", name);
                if (_carriedFlame != null)
                {
                    _carriedFlame.Extinguish(); // bypasses the toggle cooldown on death
                }
                if (_escorting != null)
                {
                    // Do not leave a villager tethered to a corpse: release it so
                    // it walks toward the nearest light on its own.
                    var villager = _escorting;
                    _escorting = null;
                    villager.ReleaseRescue();
                }
            }
        }

        /// <summary>Deterministic simulation step (autoTick = false in tests).</summary>
        public void Tick(float dt)
        {
            EnsureInit();
            if (dt <= 0f || !IsAlive)
            {
                return;
            }

            var cfg = Config;
            _bellCooldown -= dt;
            _rescueCooldown -= dt;
            _feedCooldown -= dt;
            _flameCooldown -= dt;

            if (useDirectInput && Application.isPlaying)
            {
                ReadDirectInput(cfg, dt);
            }
            else if (_moveTarget.HasValue)
            {
                transform.position = PlanarMotion.Step(
                    transform.position, _moveTarget.Value, cfg.bellkeeperMoveSpeed, dt,
                    cfg.arrivalRadius, out bool arrived);
                if (arrived)
                {
                    _moveTarget = null;
                }
            }

            if (IsCarryingFlame)
            {
                Stamina -= cfg.carriedFlameStaminaPerSecond * dt;
                if (Stamina <= 0f)
                {
                    Stamina = 0f;
                    _carriedFlame.Extinguish();
                    GameEventLog.Append("hero_flame_exhausted", name);
                }
            }
            else
            {
                Stamina = Mathf.Min(cfg.bellkeeperMaxStamina,
                    Stamina + cfg.bellkeeperStaminaRegenPerSecond * dt);
            }
        }

        void ReadDirectInput(PrototypeConfig cfg, float dt)
        {
            float x = Input.GetAxisRaw("Horizontal");
            float z = Input.GetAxisRaw("Vertical");
            if (Mathf.Approximately(x, 0f) && Mathf.Approximately(z, 0f))
            {
                return;
            }
            _moveTarget = null; // direct input overrides any queued destination

            // Camera-relative ground directions under the locked 45° yaw.
            Vector3 forward = Quaternion.Euler(0f, IsoCameraController.Yaw, 0f) * Vector3.forward;
            Vector3 right = Quaternion.Euler(0f, IsoCameraController.Yaw, 0f) * Vector3.right;
            Vector3 delta = (right * x + forward * z).normalized * cfg.bellkeeperMoveSpeed * dt;
            transform.position += delta;
        }
    }
}
