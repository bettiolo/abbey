using System.Collections.Generic;
using Abbey.Core;
using Abbey.Light;
using NUnit.Framework;
using UnityEngine;

namespace Abbey.Tests.EditMode
{
    public class DarknessEvaluatorTests
    {
        readonly List<GameObject> _spawned = new List<GameObject>();
        PrototypeConfig _config;

        [SetUp]
        public void SetUp()
        {
            EventBus.ResetAll();
            GameEventLog.Clear();
            DarknessEvaluator.Clear();

            _config = ScriptableObject.CreateInstance<PrototypeConfig>();
            _config.edgeBandFraction = 0.25f; // Safe band ends at 75% of effective radius
            DarknessEvaluator.Config = _config;
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
            DarknessEvaluator.Clear();
            EventBus.ResetAll();
            GameEventLog.Clear();
        }

        LightSource SpawnLight(Vector3 position, float radius, float strength = 1f, bool lit = true)
        {
            var go = new GameObject($"TestLight_{_spawned.Count}");
            _spawned.Add(go);
            go.transform.position = position;
            var source = go.AddComponent<LightSource>();
            source.radius = radius;
            source.strength = strength;
            source.isLit = lit;
            source.autoTick = false;
            // [ExecuteAlways] makes OnEnable register with the evaluator on AddComponent,
            // but register defensively in case the editor defers the callback.
            DarknessEvaluator.Register(source);
            return source;
        }

        [Test]
        public void SingleLitSource_ClassifiesSafeEdgeDark()
        {
            SpawnLight(Vector3.zero, radius: 10f);
            // Effective radius 10, inner (safe) radius 7.5.

            Assert.AreEqual(LightZone.Safe, DarknessEvaluator.Classify(Vector3.zero));
            Assert.AreEqual(LightZone.Safe, DarknessEvaluator.Classify(new Vector3(7f, 0f, 0f)));
            Assert.AreEqual(LightZone.Edge, DarknessEvaluator.Classify(new Vector3(8f, 0f, 0f)));
            Assert.AreEqual(LightZone.Edge, DarknessEvaluator.Classify(new Vector3(9.9f, 0f, 0f)));
            Assert.AreEqual(LightZone.Dark, DarknessEvaluator.Classify(new Vector3(10.1f, 0f, 0f)));
            Assert.AreEqual(LightZone.Dark, DarknessEvaluator.Classify(new Vector3(50f, 0f, 50f)));
        }

        [Test]
        public void StrengthScalesEffectiveRadius()
        {
            SpawnLight(Vector3.zero, radius: 10f, strength: 0.5f);
            // Effective radius 5, safe radius 3.75.

            Assert.AreEqual(LightZone.Safe, DarknessEvaluator.Classify(new Vector3(3f, 0f, 0f)));
            Assert.AreEqual(LightZone.Edge, DarknessEvaluator.Classify(new Vector3(4.5f, 0f, 0f)));
            Assert.AreEqual(LightZone.Dark, DarknessEvaluator.Classify(new Vector3(6f, 0f, 0f)));
        }

        [Test]
        public void OverlappingLights_EdgeOfOneInsideSafeOfAnother_IsSafe()
        {
            SpawnLight(Vector3.zero, radius: 10f);
            SpawnLight(new Vector3(9f, 0f, 0f), radius: 10f);

            // x = 9 is Edge of the first light (dist 9 > 7.5) but the exact center
            // of the second: Safe must win.
            Assert.AreEqual(LightZone.Safe, DarknessEvaluator.Classify(new Vector3(9f, 0f, 0f)));
        }

        [Test]
        public void NearestSafePoint_PadsTargetByArrivalRadius()
        {
            _config.edgeBandFraction = 0.3f;
            _config.arrivalRadius = 0.3f;
            var source = SpawnLight(new Vector3(3f, 0f, 0f), radius: 6f);
            var from = new Vector3(30f, 0f, 0f);

            Vector3 target = DarknessEvaluator.NearestSafePoint(from);
            Vector3 dir = PlanarMotion.Direction(target, from);
            Vector3 possibleStop = target + dir * _config.arrivalRadius;

            Assert.AreEqual(LightZone.Safe, DarknessEvaluator.Classify(target));
            Assert.AreEqual(LightZone.Safe, DarknessEvaluator.Classify(possibleStop),
                "agents may stop within arrivalRadius of the target and must still be Safe");
            Assert.LessOrEqual(
                PlanarMotion.Distance(possibleStop, source.transform.position),
                source.EffectiveRadius * (1f - _config.edgeBandFraction));
        }

        [Test]
        public void ExtinguishedLight_IsDark()
        {
            var source = SpawnLight(Vector3.zero, radius: 10f);
            Assert.AreEqual(LightZone.Safe, DarknessEvaluator.Classify(Vector3.zero));

            source.Extinguish();

            Assert.IsFalse(source.isLit);
            Assert.AreEqual(LightZone.Dark, DarknessEvaluator.Classify(Vector3.zero));
        }

        [Test]
        public void UnlitSource_IsDarkEverywhere()
        {
            SpawnLight(Vector3.zero, radius: 10f, strength: 1f, lit: false);
            Assert.AreEqual(LightZone.Dark, DarknessEvaluator.Classify(Vector3.zero));
        }

        [Test]
        public void FuelDepletion_FlipsIsLit_AndZoneGoesDark()
        {
            var source = SpawnLight(Vector3.zero, radius: 10f);
            source.fuelSeconds = 2f;
            source.fuelConsumptionPerSecond = 1f;

            source.Tick(1.5f);
            Assert.IsTrue(source.isLit, "Light should still burn with fuel remaining");
            Assert.AreEqual(LightZone.Safe, DarknessEvaluator.Classify(Vector3.zero));

            source.Tick(1f);
            Assert.IsFalse(source.isLit, "Light must extinguish when fuel runs out");
            Assert.AreEqual(0f, source.fuelSeconds);
            Assert.AreEqual(LightZone.Dark, DarknessEvaluator.Classify(Vector3.zero));
        }

        [Test]
        public void InfiniteFuel_NeverExtinguishes()
        {
            var source = SpawnLight(Vector3.zero, radius: 10f);
            source.fuelSeconds = -1f;

            source.Tick(10000f);

            Assert.IsTrue(source.isLit);
            Assert.IsTrue(source.HasInfiniteFuel);
        }

        [Test]
        public void DisabledSource_UnregistersAndGoesDark()
        {
            var source = SpawnLight(Vector3.zero, radius: 10f);
            Assert.AreEqual(LightZone.Safe, DarknessEvaluator.Classify(Vector3.zero));

            source.enabled = false;
            DarknessEvaluator.Unregister(source); // defensive, mirrors OnDisable

            Assert.AreEqual(LightZone.Dark, DarknessEvaluator.Classify(Vector3.zero));
        }

        [Test]
        public void StrongestLightAt_PicksTheBrighterContribution()
        {
            var weak = SpawnLight(Vector3.zero, radius: 10f, strength: 0.4f);
            var strong = SpawnLight(new Vector3(2f, 0f, 0f), radius: 10f, strength: 1f);

            var probe = new Vector3(1f, 0f, 0f);
            Assert.AreSame(strong, DarknessEvaluator.StrongestLightAt(probe));
            Assert.IsNull(DarknessEvaluator.StrongestLightAt(new Vector3(100f, 0f, 0f)));
            Assert.IsNotNull(weak); // silence unused warning
        }

        [Test]
        public void LightIntensity_FallsOffLinearly_AndIsZeroInDark()
        {
            SpawnLight(Vector3.zero, radius: 10f, strength: 1f);

            Assert.AreEqual(1f, DarknessEvaluator.LightIntensityAt(Vector3.zero), 1e-4f);
            Assert.AreEqual(0.5f, DarknessEvaluator.LightIntensityAt(new Vector3(5f, 0f, 0f)), 1e-4f);
            Assert.AreEqual(0f, DarknessEvaluator.LightIntensityAt(new Vector3(20f, 0f, 0f)));
        }

        [Test]
        public void NearestSafePoint_ReturnsSafePosition()
        {
            SpawnLight(Vector3.zero, radius: 10f);

            var darkPos = new Vector3(20f, 0f, 0f);
            var safePoint = DarknessEvaluator.NearestSafePoint(darkPos);

            Assert.AreEqual(LightZone.Safe, DarknessEvaluator.Classify(safePoint));
            // It should sit between the dark position and the light, on the near side.
            Assert.Less(Vector3.Distance(darkPos, safePoint), Vector3.Distance(darkPos, Vector3.zero) + 0.01f);
        }

        [Test]
        public void NearestSafePoint_AlreadySafe_ReturnsInput()
        {
            SpawnLight(Vector3.zero, radius: 10f);
            var pos = new Vector3(1f, 0f, 1f);
            Assert.AreEqual(pos, DarknessEvaluator.NearestSafePoint(pos));
        }

        [Test]
        public void ClassificationIsPlanar_IgnoresHeight()
        {
            SpawnLight(Vector3.zero, radius: 10f);
            Assert.AreEqual(LightZone.Safe, DarknessEvaluator.Classify(new Vector3(0f, 5f, 0f)));
        }
    }
}
