using Abbey.Beast;
using Abbey.Core;
using Abbey.Villagers;
using UnityEngine;

namespace Abbey.Hero
{
    /// <summary>
    /// Keyboard bindings for the Bellkeeper's abilities in the playable prototype
    /// scene. <see cref="BellkeeperController"/> deliberately exposes only APIs
    /// (tests drive them directly); this component is the thin play-mode glue:
    /// Space = ring bell, F = raise/douse the carried flame, E = rescue nearest
    /// villager / release the escorted one, H = feed the hound. Range/cost checks
    /// stay inside the controller (it refuses out-of-range calls) — this class
    /// holds key codes only, never balance values.
    /// </summary>
    [RequireComponent(typeof(BellkeeperController))]
    [DisallowMultipleComponent]
    public class BellkeeperInputBindings : MonoBehaviour
    {
        public KeyCode ringBellKey = KeyCode.Space;
        public KeyCode toggleFlameKey = KeyCode.F;
        public KeyCode rescueKey = KeyCode.E;
        public KeyCode feedHoundKey = KeyCode.H;

        BellkeeperController _hero;
        HoundController _hound;

        void Awake()
        {
            _hero = GetComponent<BellkeeperController>();
        }

        void Update()
        {
            if (!Application.isPlaying || _hero == null || !_hero.IsAlive)
            {
                return;
            }

            if (Input.GetKeyDown(ringBellKey))
            {
                _hero.RingBell();
            }

            if (Input.GetKeyDown(toggleFlameKey))
            {
                _hero.CarryFlame(!_hero.IsCarryingFlame);
            }

            if (Input.GetKeyDown(rescueKey))
            {
                if (_hero.EscortedVillager != null)
                {
                    _hero.ReleaseRescued();
                }
                else
                {
                    var nearest = FindNearestVillager();
                    if (nearest != null)
                    {
                        _hero.Rescue(nearest); // controller enforces interact range
                    }
                }
            }

            if (Input.GetKeyDown(feedHoundKey))
            {
                if (_hound == null)
                {
                    _hound = FindFirstObjectByType<HoundController>();
                }
                if (_hound != null)
                {
                    _hero.FeedHound(_hound); // controller enforces range + food
                }
            }
        }

        VillagerAgent FindNearestVillager()
        {
            VillagerAgent best = null;
            float bestDist = float.MaxValue;
            var villagers = DuskRecallSystem.Villagers;
            for (int i = 0; i < villagers.Count; i++)
            {
                var v = villagers[i];
                if (v == null || v.State == VillagerState.Missing || v.State == VillagerState.Dead)
                {
                    continue;
                }
                float dist = PlanarMotion.Distance(transform.position, v.transform.position);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = v;
                }
            }
            return best;
        }
    }
}
