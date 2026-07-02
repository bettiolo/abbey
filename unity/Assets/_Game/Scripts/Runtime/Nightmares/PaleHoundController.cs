using UnityEngine;

namespace Abbey.Nightmares
{
    /// <summary>
    /// The pale hound, named explicitly for the Phase 2 director. Behaviour is
    /// exactly the <see cref="MonsterController"/> base: tests lantern edges,
    /// avoids Safe light, targets the most isolated exposed villager, flees the
    /// Black Hound. Kept as a distinct component so the debug panel, the event
    /// log and future consequence systems can tell species apart while the 0.1
    /// director keeps spawning the base class unchanged.
    /// </summary>
    [ExecuteAlways]
    public class PaleHoundController : MonsterController
    {
        public override NightmareType Type => NightmareType.PaleHound;
    }
}
