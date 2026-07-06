using System.Collections.Generic;
using Abbey.Buildings;
using Abbey.Core;
using Abbey.Economy;
using NUnit.Framework;
using UnityEngine;

namespace Abbey.Tests.EditMode
{
    public class PlanarMotionTests
    {
        BuildingCatalog _catalog;

        [SetUp]
        public void SetUp()
        {
            ClearStatics();
            _catalog = ScriptableObject.CreateInstance<BuildingCatalog>();
            _catalog.buildings = new List<BuildingType>
            {
                new BuildingType
                {
                    id = "hut",
                    displayName = "Test Hut",
                    footprint = new Vector2(1f, 1f),
                    function = FunctionKind.Shelter,
                    cost = new List<ResourceStack>(),
                    buildWorkSeconds = 1f,
                },
            };
            BuildingPlacer.Catalog = _catalog;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var building in Object.FindObjectsByType<Building>(
                         FindObjectsInactive.Include))
            {
                Object.DestroyImmediate(building.gameObject);
            }
            foreach (var site in Object.FindObjectsByType<ConstructionSite>(
                         FindObjectsInactive.Include))
            {
                Object.DestroyImmediate(site.gameObject);
            }
            Object.DestroyImmediate(_catalog);
            ClearStatics();
        }

        [Test]
        public void StepAroundBuildings_DetoursAroundCompletedFootprint()
        {
            CreateBuilding(Vector3.zero);

            var from = new Vector3(-2f, 0f, 0f);
            var target = new Vector3(2f, 0f, 0f);
            var next = PlanarMotion.StepAroundBuildings(
                from, target, 2f, 1f, 0.1f, 0.2f, out bool arrived);

            Assert.IsFalse(arrived);
            Assert.IsFalse(PlanarMotion.IsInsideBuildingFootprint(next, 0.2f));
            Assert.Greater(Mathf.Abs(next.z), 0.01f,
                "a straight line would enter the hut, so the step should bend around it");
        }

        [Test]
        public void StepAroundBuildings_DetoursAroundConstructionSiteFootprint()
        {
            ResourceLedger.Add(ResourceType.Wood, 99, "test");
            Assert.IsNotNull(BuildingPlacer.PlaceConstructionSite("hut", Vector3.zero));

            var from = new Vector3(-2f, 0f, 0f);
            var target = new Vector3(2f, 0f, 0f);
            var next = PlanarMotion.StepAroundBuildings(
                from, target, 2f, 1f, 0.1f, 0.2f, out _);

            Assert.IsFalse(PlanarMotion.IsInsideBuildingFootprint(next, 0.2f));
            Assert.Greater(Mathf.Abs(next.z), 0.01f);
        }

        [Test]
        public void MoveAroundBuildings_BlocksDirectInputFromEnteringFootprint()
        {
            CreateBuilding(Vector3.zero);

            var from = new Vector3(-1f, 0f, 0f);
            var next = PlanarMotion.MoveAroundBuildings(
                from, new Vector3(0.8f, 0f, 0f), 0.2f);

            Assert.IsFalse(PlanarMotion.IsInsideBuildingFootprint(next, 0.2f));
        }

        [Test]
        public void StepAroundBuildings_TargetInsideCompletedFootprintArrivesAtOutsideEdge()
        {
            CreateBuilding(Vector3.zero);

            var from = new Vector3(-2f, 0f, 0f);
            var next = PlanarMotion.StepAroundBuildings(
                from, Vector3.zero, 10f, 1f, 0.3f, 0.2f, out bool arrived);

            Assert.IsTrue(arrived);
            Assert.IsFalse(PlanarMotion.IsInsideBuildingFootprint(next, 0.2f));
            Assert.Less(next.x, -0.7f);
        }

        [Test]
        public void StepAroundBuildings_TargetInsideConstructionSiteArrivesAtOutsideEdge()
        {
            ResourceLedger.Add(ResourceType.Wood, 99, "test");
            Assert.IsNotNull(BuildingPlacer.PlaceConstructionSite("hut", Vector3.zero));

            var from = new Vector3(0f, 0f, -2f);
            var next = PlanarMotion.StepAroundBuildings(
                from, Vector3.zero, 10f, 1f, 0.3f, 0.2f, out bool arrived);

            Assert.IsTrue(arrived);
            Assert.IsFalse(PlanarMotion.IsInsideBuildingFootprint(next, 0.2f));
            Assert.Less(next.z, -0.7f);
        }

        void CreateBuilding(Vector3 position)
        {
            var go = new GameObject("hut");
            go.transform.position = position;
            var building = go.AddComponent<Building>();
            building.Initialize(_catalog.Find("hut"));
        }

        static void ClearStatics()
        {
            Building.ClearRegistry();
            ConstructionSite.ClearRegistry();
            BuildingPlacer.Clear();
            BuildingCatalog.ClearCache();
            ResourceLedger.Clear();
            EconomyConfig.ClearCache();
            PrototypeConfig.ClearCache();
            GameEventLog.Clear();
        }
    }
}
