using Abbey.UI;
using NUnit.Framework;
using UnityEngine;

namespace Abbey.Tests.EditMode
{
    public class HudHintDockTests
    {
        static readonly Vector2[] Resolutions =
        {
            new Vector2(640f, 360f),
            new Vector2(1028f, 768f),
            new Vector2(1920f, 1080f),
        };

        [Test]
        public void EveryHintHasANonOverlappingSlotAtSupportedResolutions()
        {
            foreach (Vector2 resolution in Resolutions)
            {
                for (int i = 0; i < HudHintDock.SlotCount; i++)
                {
                    Rect current = HudHintDock.RectFor(
                        (HudHintSlot)i, resolution.x, resolution.y);
                    Assert.GreaterOrEqual(current.xMin, 0f);
                    Assert.GreaterOrEqual(current.yMin, 0f);
                    Assert.LessOrEqual(current.xMax, resolution.x);
                    Assert.LessOrEqual(current.yMax, resolution.y);

                    for (int j = 0; j < i; j++)
                    {
                        Rect previous = HudHintDock.RectFor(
                            (HudHintSlot)j, resolution.x, resolution.y);
                        Assert.IsFalse(current.Overlaps(previous),
                            $"slots {i} and {j} overlap at {resolution.x}x{resolution.y}");
                    }
                }
            }
        }
    }
}
