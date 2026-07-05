using System.Collections.Generic;
using Abbey.Light;
using Abbey.Villagers;
using UnityEngine;

namespace Abbey.Decrees
{
    /// <summary>
    /// The seven emergency overdrive levers (ROADMAP Phase 3 item 16). Each buys
    /// immediate night capability and books a cost — some paid now (resources, sanity,
    /// trust, beast status), some deferred (a nightmare debt the director cashes in on
    /// later nights). Order is stable (serialized indices must not shift).
    /// </summary>
    public enum OverdriveActionId
    {
        /// <summary>A job site keeps working through the night: big sanity + trust cost.</summary>
        ForcedNightWork,

        /// <summary>Villagers carry candles as mobile lights along a road / worksite.</summary>
        CandleLine,

        /// <summary>Selected lanterns burn brighter (radius up) at a multiplied fuel rate.</summary>
        LanternOverburn,

        /// <summary>Sustained bell: recall boost + monster hesitation; trust cost if overused.</summary>
        BellToll,

        /// <summary>Sanctity-charged calm aura near the abbey; candle cost + old-faith debt.</summary>
        AbbeyRite,

        /// <summary>Send the hound to hunt proactively: beast-status cost + hound pain/fear risk.</summary>
        HoundHunt,

        /// <summary>Trusted villagers stand watch armed: sanity cost, a small combat contribution.</summary>
        VolunteerWatch
    }

    /// <summary>
    /// The live per-night state of a single fired overdrive lever, held by
    /// <see cref="OverdriveSystem"/>. Pure runtime bookkeeping — all balance lives in
    /// <see cref="OverdriveConfig"/>. Tracks the participants (for the dawn deferred-dread
    /// settlement and the recall exemption hand-back), the mobile candle lights and the
    /// lanterns it overburned (both restored/despawned at dawn), the running upkeep timer
    /// and the nightmare-debt points this lever will settle at dawn.
    /// </summary>
    public class OverdriveAction
    {
        public readonly OverdriveActionId Id;

        /// <summary>The day the lever was fired (cooldown accounting).</summary>
        public readonly int ActivatedDay;

        /// <summary>Villagers this lever pressed into night service (deferred dread + recall hand-back).</summary>
        public readonly List<VillagerAgent> Participants = new List<VillagerAgent>();

        /// <summary>Mobile candle lights this lever spawned (despawned at dawn).</summary>
        public readonly List<LightSource> CandleLights = new List<LightSource>();

        /// <summary>Lanterns this lever pushed into overburn (radius/fuel restored at dawn).</summary>
        public readonly List<LightSource> OverburnedLanterns = new List<LightSource>();

        /// <summary>The data this lever was fired from (deferred-dread + debt settlement read it).</summary>
        public readonly OverdriveActionDef Def;

        /// <summary>Nightmare-debt points booked at activation, settled into the pending pool at dawn.</summary>
        public float NightmareDebtPoints => Def != null ? Def.nightmareDebtPoints : 0f;

        /// <summary>Running upkeep accumulator (seconds since the last upkeep drain).</summary>
        public float UpkeepTimer;

        /// <summary>False once the lever stood down (out of upkeep resources, or dawn).</summary>
        public bool Active = true;

        public OverdriveAction(OverdriveActionId id, int activatedDay, OverdriveActionDef def)
        {
            Id = id;
            ActivatedDay = activatedDay;
            Def = def;
        }
    }

    /// <summary>
    /// Optional per-activation context: where the candle line runs and which lanterns to
    /// overburn. A default context (built from the system's own transform + every
    /// non-sacred light) is used when the caller supplies none, so the debug panel can
    /// fire a lever with one keypress.
    /// </summary>
    public struct OverdriveContext
    {
        /// <summary>Candle-line route start (carriers are spread from here to <see cref="RouteEnd"/>).</summary>
        public Vector3 RouteStart;

        /// <summary>Candle-line route end.</summary>
        public Vector3 RouteEnd;

        /// <summary>Lanterns to overburn (null ⇒ every registered non-sacred light).</summary>
        public IReadOnlyList<LightSource> Lanterns;

        public OverdriveContext(Vector3 routeStart, Vector3 routeEnd, IReadOnlyList<LightSource> lanterns = null)
        {
            RouteStart = routeStart;
            RouteEnd = routeEnd;
            Lanterns = lanterns;
        }
    }
}
