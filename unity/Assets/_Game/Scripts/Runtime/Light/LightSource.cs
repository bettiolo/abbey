using Abbey.Core;
using UnityEngine;

namespace Abbey.Light
{
    /// <summary>
    /// A gameplay light: campfire, lantern post, carried flame, sacred abbey fire.
    /// Registers with <see cref="DarknessEvaluator"/> while enabled. Fuel burns down
    /// over time; when it runs out the light dies (darkness is territory).
    /// [ExecuteAlways] so EditMode tests get OnEnable/OnDisable registration.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class LightSource : MonoBehaviour
    {
        [Tooltip("Full territory radius in world units at strength 1.")]
        [Min(0f)] public float radius = 5f;

        [Tooltip("0..1. Effective radius = radius * strength.")]
        [Range(0f, 1f)] public float strength = 1f;

        public bool isLit = true;

        [Tooltip("Sacred lights (abbey flame, bell tower) matter for win/loss and the hound.")]
        public bool sacred;

        [Tooltip("Seconds of fuel remaining. Negative = infinite (never burns out).")]
        public float fuelSeconds = -1f;

        [Tooltip("Fuel drained per simulated second while lit.")]
        [Min(0f)] public float fuelConsumptionPerSecond = 1f;

        [Tooltip("Advance fuel from Update using Time.deltaTime. Tests set false and call Tick().")]
        public bool autoTick = true;

        /// <summary>Territory radius after strength scaling; 0 when unlit.</summary>
        public float EffectiveRadius => isLit ? radius * Mathf.Clamp01(strength) : 0f;

        public bool HasInfiniteFuel => fuelSeconds < 0f;

        // ---- P3-08 overdrive: temporary "burn brighter, burn faster" mode ----
        bool _overburning;
        float _baseRadius;
        float _baseFuelRate;

        /// <summary>True while an overdrive Lantern Overburn is boosting this light.</summary>
        public bool IsOverburning => _overburning;

        void OnEnable()
        {
            DarknessEvaluator.Register(this);
        }

        void OnDisable()
        {
            DarknessEvaluator.Unregister(this);
        }

        void Update()
        {
            if (!Application.isPlaying || !autoTick)
            {
                return;
            }
            Tick(Time.deltaTime);
        }

        /// <summary>Burns fuel for dt seconds; extinguishes the light when fuel hits zero.</summary>
        public void Tick(float dt)
        {
            if (!isLit || HasInfiniteFuel || dt <= 0f)
            {
                return;
            }

            fuelSeconds -= fuelConsumptionPerSecond * dt;
            if (fuelSeconds <= 0f)
            {
                fuelSeconds = 0f;
                Extinguish();
            }
        }

        public void Extinguish()
        {
            if (!isLit)
            {
                return;
            }
            isLit = false;
            GameEventLog.Append("LightExtinguished", $"{name} sacred={sacred}");
        }

        /// <summary>
        /// Relights the source. Optionally adds fuel (ignored if infinite). A burned-out
        /// source (finite fuel, zero remaining) refuses to relight until it is given
        /// fuel — darkness must cost something to push back.
        /// </summary>
        public void Ignite(float addedFuelSeconds = 0f)
        {
            if (!HasInfiniteFuel && addedFuelSeconds > 0f)
            {
                fuelSeconds += addedFuelSeconds;
            }
            if (isLit)
            {
                return;
            }
            if (!HasInfiniteFuel && fuelSeconds <= 0f)
            {
                GameEventLog.Append("LightIgniteFailed", $"{name} no fuel");
                return;
            }
            isLit = true;
            GameEventLog.Append("LightIgnited", $"{name} sacred={sacred}");
        }

        /// <summary>
        /// Lantern Overburn (P3-08): the light burns brighter — radius scaled up — at a
        /// multiplied fuel-consumption rate, so it eats its fuel faster and may gutter out
        /// mid-night. Idempotent: re-applying restores from the original values first, so
        /// the multipliers never stack. Multipliers below 1 are clamped to 1 (overburn
        /// only ever brightens/burns faster).
        /// </summary>
        public void ApplyOverburn(float radiusMultiplier, float fuelRateMultiplier)
        {
            if (!_overburning)
            {
                _baseRadius = radius;
                _baseFuelRate = fuelConsumptionPerSecond;
                _overburning = true;
            }
            radius = _baseRadius * Mathf.Max(1f, radiusMultiplier);
            fuelConsumptionPerSecond = _baseFuelRate * Mathf.Max(1f, fuelRateMultiplier);
            GameEventLog.Append("light_overburn",
                $"{name} radius={radius:F1} fuelRate={fuelConsumptionPerSecond:F2}");
        }

        /// <summary>Ends overburn, restoring the pre-overburn radius and fuel rate. No-op when not overburning.</summary>
        public void ClearOverburn()
        {
            if (!_overburning)
            {
                return;
            }
            _overburning = false;
            radius = _baseRadius;
            fuelConsumptionPerSecond = _baseFuelRate;
            GameEventLog.Append("light_overburn", $"{name} cleared radius={radius:F1}");
        }

        void OnDrawGizmosSelected()
        {
            if (!isLit)
            {
                Gizmos.color = Color.gray;
                Gizmos.DrawWireSphere(transform.position, radius);
                return;
            }
            float edgeFraction = DarknessEvaluator.EdgeBandFraction;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, EffectiveRadius);
            Gizmos.color = new Color(1f, 0.6f, 0f);
            Gizmos.DrawWireSphere(transform.position, EffectiveRadius * (1f - edgeFraction));
        }
    }
}
