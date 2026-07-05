using Abbey.Core;
using NUnit.Framework;
using UnityEngine;

namespace Abbey.Tests.EditMode
{
    public class PrototypeConfigTests
    {
        [SetUp]
        public void SetUp()
        {
            PrototypeConfig.ClearCache();
        }

        [TearDown]
        public void TearDown()
        {
            PrototypeConfig.ClearCache();
        }

        [Test]
        public void LoadOrDefault_NeverReturnsNull()
        {
            var config = PrototypeConfig.LoadOrDefault();
            Assert.IsNotNull(config);
        }

        [Test]
        public void LoadOrDefault_IsCached()
        {
            Assert.AreSame(PrototypeConfig.LoadOrDefault(), PrototypeConfig.LoadOrDefault());
        }

        [Test]
        public void Defaults_AreSane()
        {
            var c = PrototypeConfig.LoadOrDefault();

            Assert.Greater(c.dayDurationSeconds, 0f);
            Assert.Greater(c.duskDurationSeconds, 0f);
            Assert.Greater(c.nightDurationSeconds, 0f);
            Assert.Greater(c.dawnDurationSeconds, 0f);

            Assert.Greater(c.edgeBandFraction, 0f);
            Assert.Less(c.edgeBandFraction, 1f);
            Assert.Greater(c.campfireRadius, 0f);
            Assert.That(c.campfireStrength, Is.InRange(0f, 1f));
            Assert.Greater(c.lanternRadius, 0f);
            Assert.Greater(c.carriedFlameRadius, 0f);
            Assert.Greater(c.sacredFlameRadius, 0f);
            Assert.GreaterOrEqual(c.fuelConsumptionPerSecond, 0f);

            Assert.Greater(c.bellkeeperMoveSpeed, 0f);
            Assert.Greater(c.bellkeeperSprintMultiplier, 1f);

            Assert.Greater(c.villagerWalkSpeed, 0f);
            Assert.GreaterOrEqual(c.villagerPanicSpeed, c.villagerWalkSpeed);
            Assert.That(c.villagerBraveryMin, Is.InRange(0f, 1f));
            Assert.That(c.villagerBraveryMax, Is.InRange(0f, 1f));
            Assert.LessOrEqual(c.villagerBraveryMin, c.villagerBraveryMax);

            Assert.That(c.trustFedThreshold, Is.InRange(0f, 1f));
            Assert.That(c.trustFollowThreshold, Is.InRange(0f, 1f));
            Assert.GreaterOrEqual(c.trustFollowThreshold, c.trustFedThreshold);
            Assert.That(c.hungerStarvingThreshold, Is.InRange(0f, 1f));
            Assert.Greater(c.feedTrustGain, 0f);
            Assert.Greater(c.feedHungerRelief, 0f);

            Assert.Greater(c.monsterMoveSpeed, 0f);
            Assert.GreaterOrEqual(c.monsterFleeSpeed, c.monsterMoveSpeed);
            Assert.That(c.monsterLightTolerance, Is.InRange(0f, 1f));

            Assert.Greater(c.bellRadius, 0f);
            Assert.GreaterOrEqual(c.bellCooldownSeconds, 0f);

            Assert.Greater(c.cameraPanSpeed, 0f);
            Assert.Greater(c.cameraZoomSpeed, 0f);
            Assert.Greater(c.cameraMinOrthoSize, 0f);
            Assert.Greater(c.cameraMaxOrthoSize, c.cameraMinOrthoSize);
            Assert.That(c.cameraDefaultOrthoSize,
                Is.InRange(c.cameraMinOrthoSize, c.cameraMaxOrthoSize));
        }

        [Test]
        public void BellRadius_CoversMoreThanCampfire()
        {
            // The recall bell must reach beyond the campfire's territory or the
            // dusk recall mechanic cannot work.
            var c = PrototypeConfig.LoadOrDefault();
            Assert.Greater(c.bellRadius, c.campfireRadius);
        }
    }
}
