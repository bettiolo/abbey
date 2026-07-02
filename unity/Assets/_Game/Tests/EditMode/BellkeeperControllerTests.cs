using System.Collections.Generic;
using Abbey.Beast;
using Abbey.Core;
using Abbey.Hero;
using Abbey.Light;
using Abbey.Villagers;
using NUnit.Framework;
using UnityEngine;

namespace Abbey.Tests.EditMode
{
    /// <summary>
    /// Bellkeeper ability cooldowns and health/stamina math: bell cooldown, carried
    /// flame stamina drain + auto-extinguish, regen cap, feed/rescue range checks,
    /// and what dying cleans up. All values are injected via PrototypeConfig.
    /// </summary>
    public class BellkeeperControllerTests
    {
        readonly List<GameObject> _spawned = new List<GameObject>();
        PrototypeConfig _config;
        BellkeeperController _hero;

        [SetUp]
        public void SetUp()
        {
            EventBus.ResetAll();
            GameEventLog.Clear();
            DarknessEvaluator.Clear();
            DuskRecallSystem.Clear();

            _config = ScriptableObject.CreateInstance<PrototypeConfig>();
            _config.bellkeeperMoveSpeed = 4f;
            _config.bellkeeperMaxHealth = 100f;
            _config.bellkeeperMaxStamina = 100f;
            _config.bellkeeperStaminaRegenPerSecond = 4f;
            _config.carriedFlameStaminaPerSecond = 2f;
            _config.interactRange = 2f;
            _config.rescueCooldownSeconds = 0.5f;
            _config.feedCooldownSeconds = 0.5f;
            _config.carryFlameCooldownSeconds = 0.25f;
            _config.startingCarriedFood = 3;
            _config.bellRadius = 15f;
            _config.bellCooldownSeconds = 5f;
            _config.arrivalRadius = 0.3f;
            _config.carriedFlameRadius = 3f;
            _config.carriedFlameStrength = 0.6f;
            _config.feedTrustGain = 0.2f;
            _config.houndStartTrust = 0.2f;
            _config.houndStartHunger = 0.9f;
            _config.feedHungerRelief = 0.3f;

            DarknessEvaluator.Config = _config;
            DuskRecallSystem.Config = _config;

            var go = new GameObject("TestHero");
            _spawned.Add(go);
            _hero = go.AddComponent<BellkeeperController>();
            _hero.autoTick = false;
            _hero.useDirectInput = false;
            _hero.Configure(_config);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }
            _spawned.Clear();
            Object.DestroyImmediate(_config);
            DuskRecallSystem.Clear();
            DarknessEvaluator.Clear();
            EventBus.ResetAll();
            GameEventLog.Clear();
        }

        HoundController SpawnHound(Vector3 position)
        {
            var go = new GameObject("TestHound");
            _spawned.Add(go);
            go.transform.position = position;
            var hound = go.AddComponent<HoundController>();
            hound.autoTick = false;
            hound.Configure(_config);
            return hound;
        }

        VillagerAgent SpawnVillager(Vector3 position)
        {
            var go = new GameObject($"TestVillager_{_spawned.Count}");
            _spawned.Add(go);
            go.transform.position = position;
            var villager = go.AddComponent<VillagerAgent>();
            villager.autoTick = false;
            villager.Config = _config;
            return villager;
        }

        [Test]
        public void Init_ReadsHealthStaminaFood_FromConfig()
        {
            Assert.AreEqual(100f, _hero.Health);
            Assert.AreEqual(100f, _hero.Stamina);
            Assert.AreEqual(3, _hero.CarriedFood);
            Assert.IsTrue(_hero.IsAlive);
            Assert.IsFalse(_hero.IsCarryingFlame);
        }

        [Test]
        public void RingBell_RaisesEventWithConfigRadius_AndRespectsCooldown()
        {
            _hero.transform.position = new Vector3(3f, 0f, 4f);
            Vector3 pos = Vector3.zero;
            float radius = 0f;
            int rings = 0;
            EventBus.BellRang += (p, r) => { pos = p; radius = r; rings++; };

            Assert.IsTrue(_hero.RingBell());
            Assert.AreEqual(new Vector3(3f, 0f, 4f), pos);
            Assert.AreEqual(_config.bellRadius, radius);

            Assert.IsFalse(_hero.RingBell(), "bell is on a 5s cooldown");
            _hero.Tick(2f);
            Assert.IsFalse(_hero.RingBell(), "cooldown not elapsed yet");
            _hero.Tick(3.1f);
            Assert.IsTrue(_hero.RingBell(), "cooldown elapsed");
            Assert.AreEqual(2, rings);
        }

        [Test]
        public void CarryFlame_DrainsStamina_AndSelfExtinguishesAtZero()
        {
            Assert.IsTrue(_hero.CarryFlame(true));
            Assert.IsTrue(_hero.IsCarryingFlame);

            _hero.Tick(10f); // 2 stamina/s
            Assert.AreEqual(80f, _hero.Stamina, 1e-3f);
            Assert.IsTrue(_hero.IsCarryingFlame);

            _hero.Tick(45f); // would drain 90 more
            Assert.AreEqual(0f, _hero.Stamina);
            Assert.IsFalse(_hero.IsCarryingFlame, "flame dies with the hero's stamina");

            bool exhausted = false;
            foreach (var record in GameEventLog.Records)
            {
                exhausted |= record.Type == "hero_flame_exhausted";
            }
            Assert.IsTrue(exhausted);
        }

        [Test]
        public void Stamina_RegensWhenNotCarrying_AndCapsAtMax()
        {
            _hero.CarryFlame(true);
            _hero.Tick(60f); // drains all 100 stamina, flame out
            Assert.AreEqual(0f, _hero.Stamina);

            _hero.Tick(5f); // 4/s regen
            Assert.AreEqual(20f, _hero.Stamina, 1e-3f);

            _hero.Tick(1000f);
            Assert.AreEqual(_config.bellkeeperMaxStamina, _hero.Stamina, "regen caps at max");
        }

        [Test]
        public void CarryFlame_ToggleCooldown_BlocksRapidFlickering()
        {
            Assert.IsTrue(_hero.CarryFlame(true));
            Assert.IsFalse(_hero.CarryFlame(false), "toggle is on a 0.25s cooldown");
            Assert.IsTrue(_hero.IsCarryingFlame, "the refused douse leaves the flame lit");

            _hero.Tick(0.3f);
            Assert.IsTrue(_hero.CarryFlame(false));
            Assert.IsFalse(_hero.IsCarryingFlame);
        }

        [Test]
        public void CarryFlame_CreatesAMobileLightSource()
        {
            _hero.transform.position = new Vector3(50f, 0f, 50f);
            Assert.AreEqual(LightZone.Dark, DarknessEvaluator.Classify(_hero.transform.position));

            _hero.CarryFlame(true);

            Assert.AreNotEqual(LightZone.Dark,
                DarknessEvaluator.Classify(_hero.transform.position),
                "the carried flame is territory too");
        }

        [Test]
        public void FeedHound_ConsumesFood_ChecksRange_AndCooldown()
        {
            var hound = SpawnHound(new Vector3(10f, 0f, 0f));

            Assert.IsFalse(_hero.FeedHound(hound), "hound out of interact range");
            Assert.AreEqual(3, _hero.CarriedFood);

            hound.transform.position = new Vector3(1f, 0f, 0f);
            Assert.IsTrue(_hero.FeedHound(hound));
            Assert.AreEqual(2, _hero.CarriedFood);
            Assert.AreEqual(0.4f, hound.Trust, 1e-5f, "feeding routed through HoundController.Feed");

            Assert.IsFalse(_hero.FeedHound(hound), "feed is on a 0.5s cooldown");
            _hero.Tick(0.6f);
            Assert.IsTrue(_hero.FeedHound(hound));
            _hero.Tick(0.6f);
            Assert.IsTrue(_hero.FeedHound(hound));
            Assert.AreEqual(0, _hero.CarriedFood);

            _hero.Tick(0.6f);
            Assert.IsFalse(_hero.FeedHound(hound), "no food left");
        }

        [Test]
        public void Rescue_ChecksRange_AndEscortsOneVillagerAtATime()
        {
            var far = SpawnVillager(new Vector3(10f, 0f, 0f));
            var near = SpawnVillager(new Vector3(1f, 0f, 0f));
            var second = SpawnVillager(new Vector3(0f, 0f, 1f));

            Assert.IsFalse(_hero.Rescue(far), "villager out of interact range");
            Assert.IsTrue(_hero.Rescue(near));
            Assert.AreSame(near, _hero.EscortedVillager);
            Assert.IsTrue(near.IsEscorted);

            _hero.Tick(0.6f); // clear the rescue cooldown; still escorting
            Assert.IsFalse(_hero.Rescue(second), "one escort at a time");
        }

        [Test]
        public void ReleaseRescued_InSafeLight_CompletesTheRescue()
        {
            var lightGO = new GameObject("TestLight");
            _spawned.Add(lightGO);
            var light = lightGO.AddComponent<LightSource>();
            light.radius = 10f;
            light.strength = 1f;
            light.autoTick = false;
            DarknessEvaluator.Register(light); // defensive, mirrors OnEnable

            var villager = SpawnVillager(new Vector3(1f, 0f, 0f));
            GameObject rescued = null;
            EventBus.VillagerRescued += go => rescued = go;

            Assert.IsTrue(_hero.Rescue(villager));
            Assert.IsTrue(_hero.ReleaseRescued(), "released inside Safe light");
            Assert.AreSame(villager.gameObject, rescued);
            Assert.IsNull(_hero.EscortedVillager);
            Assert.AreEqual(VillagerState.Idle, villager.State);
        }

        [Test]
        public void Death_DisablesAbilities_ExtinguishesFlame_ReleasesEscort()
        {
            var villager = SpawnVillager(new Vector3(1f, 0f, 0f));
            _hero.CarryFlame(true);
            _hero.Tick(0.3f); // clear the flame toggle cooldown
            _hero.Rescue(villager);

            _hero.TakeDamage(_config.bellkeeperMaxHealth);

            Assert.IsFalse(_hero.IsAlive);
            Assert.IsFalse(_hero.IsCarryingFlame, "death douses the carried flame");
            Assert.IsNull(_hero.EscortedVillager, "death releases the escorted villager");
            Assert.IsFalse(villager.IsEscorted);
            Assert.IsFalse(_hero.RingBell(), "the dead ring no bells");

            bool died = false;
            foreach (var record in GameEventLog.Records)
            {
                died |= record.Type == "hero_died";
            }
            Assert.IsTrue(died);
        }

        [Test]
        public void MoveTarget_WalksThereAndStops()
        {
            _hero.SetMoveTarget(new Vector3(4f, 0f, 0f));
            Assert.IsTrue(_hero.HasMoveTarget);

            _hero.Tick(1f); // speed 4 covers the full distance

            Assert.LessOrEqual(
                PlanarMotion.Distance(_hero.transform.position, new Vector3(4f, 0f, 0f)),
                _config.arrivalRadius + 1e-3f);
            Assert.IsFalse(_hero.HasMoveTarget, "arrival clears the target");
        }
    }
}
