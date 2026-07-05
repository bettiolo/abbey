using Abbey.Core;
using Abbey.UI;
using NUnit.Framework;
using UnityEngine;

namespace Abbey.Tests.EditMode
{
    public class MinimapPanelTests
    {
        static readonly Vector2 WorldMin = new Vector2(-50f, -50f);
        static readonly Vector2 WorldMax = new Vector2(50f, 50f);
        static readonly Rect MapRect = new Rect(100f, 20f, 160f, 160f);

        [Test]
        public void WorldToMap_CenterOfWorldMapsToCenterOfRect()
        {
            var p = MinimapPanel.WorldToMap(Vector3.zero, MapRect, WorldMin, WorldMax);
            Assert.AreEqual(MapRect.center.x, p.x, 0.001f);
            Assert.AreEqual(MapRect.center.y, p.y, 0.001f);
        }

        [Test]
        public void WorldToMap_NorthEastCornerMapsToTopRight()
        {
            // GUI y grows downward, so world +Z (north) is the TOP of the map.
            var p = MinimapPanel.WorldToMap(new Vector3(50f, 0f, 50f),
                MapRect, WorldMin, WorldMax);
            Assert.AreEqual(MapRect.xMax, p.x, 0.001f);
            Assert.AreEqual(MapRect.yMin, p.y, 0.001f);
        }

        [Test]
        public void WorldToMap_SouthWestCornerMapsToBottomLeft()
        {
            var p = MinimapPanel.WorldToMap(new Vector3(-50f, 0f, -50f),
                MapRect, WorldMin, WorldMax);
            Assert.AreEqual(MapRect.xMin, p.x, 0.001f);
            Assert.AreEqual(MapRect.yMax, p.y, 0.001f);
        }

        [Test]
        public void WorldToMap_ClampsPositionsOutsideWorldBounds()
        {
            var p = MinimapPanel.WorldToMap(new Vector3(900f, 0f, -900f),
                MapRect, WorldMin, WorldMax);
            Assert.AreEqual(MapRect.xMax, p.x, 0.001f);
            Assert.AreEqual(MapRect.yMax, p.y, 0.001f);
        }

        [Test]
        public void MapPixelToWorld_RoundTripsThroughWorldToMap()
        {
            const int res = 96;
            var world = MinimapPanel.MapPixelToWorld(24, 72, res, WorldMin, WorldMax);
            var mapped = MinimapPanel.WorldToMap(world, new Rect(0f, 0f, res, res),
                WorldMin, WorldMax);

            // Pixel centers land back on themselves (texture row 0 = map bottom,
            // so the GUI-space y is flipped).
            Assert.AreEqual(24.5f, mapped.x, 0.001f);
            Assert.AreEqual(res - 72.5f, mapped.y, 0.001f);
        }

        [Test]
        public void GroundColorFor_NightIsDarkerThanDay()
        {
            var day = MinimapPanel.GroundColorFor(DayPhase.Day);
            var night = MinimapPanel.GroundColorFor(DayPhase.Night);
            Assert.Less(night.grayscale, day.grayscale);
        }

        [Test]
        public void GroundColorFor_EveryPhaseHasADistinctTint()
        {
            var day = MinimapPanel.GroundColorFor(DayPhase.Day);
            var dusk = MinimapPanel.GroundColorFor(DayPhase.Dusk);
            var night = MinimapPanel.GroundColorFor(DayPhase.Night);
            var dawn = MinimapPanel.GroundColorFor(DayPhase.Dawn);
            Assert.AreNotEqual(day, dusk);
            Assert.AreNotEqual(day, night);
            Assert.AreNotEqual(day, dawn);
            Assert.AreNotEqual(dusk, night);
            Assert.AreNotEqual(dusk, dawn);
            Assert.AreNotEqual(night, dawn);
        }
    }
}
