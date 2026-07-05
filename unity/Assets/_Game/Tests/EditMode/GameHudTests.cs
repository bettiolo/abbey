using Abbey.Core;
using Abbey.UI;
using Abbey.Villagers;
using NUnit.Framework;

namespace Abbey.Tests.EditMode
{
    public class GameHudTests
    {
        [Test]
        public void FormatClock_FormatsDayPhaseAndPercent()
        {
            Assert.AreEqual("Day 3 — Night 50%", GameHud.FormatClock(3, DayPhase.Night, 0.5f));
        }

        [Test]
        public void FormatClock_ClampsProgressToValidPercent()
        {
            Assert.AreEqual("Day 1 — Day 100%", GameHud.FormatClock(1, DayPhase.Day, 1.7f));
            Assert.AreEqual("Day 1 — Day 0%", GameHud.FormatClock(1, DayPhase.Day, -0.3f));
        }

        [Test]
        public void FormatStockLine_ShowsEveryTrackedResourceAndCapacity()
        {
            string line = GameHud.FormatStockLine(12, 8, 4, 2, 26, 60);
            StringAssert.Contains("Wood 12", line);
            StringAssert.Contains("Food 8", line);
            StringAssert.Contains("Oil 4", line);
            StringAssert.Contains("Medicine 2", line);
            StringAssert.Contains("Stored 26/60", line);
        }

        [Test]
        public void CountsAsLiving_ExcludesDeadAndMissing()
        {
            Assert.IsFalse(GameHud.CountsAsLiving(VillagerState.Dead));
            Assert.IsFalse(GameHud.CountsAsLiving(VillagerState.Missing));
        }

        [Test]
        public void CountsAsLiving_IncludesEveryOtherState()
        {
            Assert.IsTrue(GameHud.CountsAsLiving(VillagerState.Idle));
            Assert.IsTrue(GameHud.CountsAsLiving(VillagerState.Working));
            Assert.IsTrue(GameHud.CountsAsLiving(VillagerState.Panicking));
            Assert.IsTrue(GameHud.CountsAsLiving(VillagerState.Injured));
            Assert.IsTrue(GameHud.CountsAsLiving(VillagerState.Resting));
        }
    }
}
