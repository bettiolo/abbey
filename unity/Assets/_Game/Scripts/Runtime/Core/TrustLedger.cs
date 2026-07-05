using System;

namespace Abbey.Core
{
    /// <summary>
    /// A tiny signed accumulator standing in for the Bellkeeper-trust pressure until
    /// P3-10 builds the full moral-pressure model (which replaces this). Systems that
    /// spend or earn trust — the P3-08 overdrive levers today — write a delta here; the
    /// running value is display / downstream only. Static registry mirroring
    /// <see cref="GameEventLog"/>; every change lands in the log as a "trust_pressure"
    /// record. Tests call <see cref="Clear"/> in [SetUp] for isolation.
    /// </summary>
    public static class TrustLedger
    {
        /// <summary>Accumulated Bellkeeper-trust pressure (signed). 0 at a fresh start.</summary>
        public static float Trust { get; private set; }

        /// <summary>Raised after a change: (delta, new value).</summary>
        public static event Action<float, float> Changed;

        /// <summary>Applies a signed trust delta and logs it. No-op for a zero delta.</summary>
        public static void Add(float delta, string reason)
        {
            if (delta == 0f)
            {
                return;
            }
            Trust += delta;
            GameEventLog.Append("trust_pressure", $"{delta:+0.00;-0.00} -> {Trust:F2} ({reason})");
            Changed?.Invoke(delta, Trust);
        }

        /// <summary>Drops the accumulated pressure and subscribers (test isolation).</summary>
        public static void Clear()
        {
            Trust = 0f;
            Changed = null;
        }
    }

    /// <summary>
    /// The beast-status counterpart of <see cref="TrustLedger"/>: a signed accumulator
    /// for how the settlement's actions push its standing toward the Black Hound
    /// (-feared … +beloved). The P3-07 <c>HoundEvolutionSystem</c> owns the authoritative
    /// path-derived beast status; this ledger records the extra *pressure* an action
    /// (Hound Hunt today) spends, a stub P3-10 folds into the pressure model. Logged as
    /// "beast_status_pressure"; <see cref="Clear"/> resets it for tests.
    /// </summary>
    public static class BeastStatusLedger
    {
        /// <summary>Accumulated beast-status pressure (signed). 0 at a fresh start.</summary>
        public static float BeastStatus { get; private set; }

        /// <summary>Raised after a change: (delta, new value).</summary>
        public static event Action<float, float> Changed;

        /// <summary>Applies a signed beast-status delta and logs it. No-op for a zero delta.</summary>
        public static void Add(float delta, string reason)
        {
            if (delta == 0f)
            {
                return;
            }
            BeastStatus += delta;
            GameEventLog.Append("beast_status_pressure",
                $"{delta:+0.00;-0.00} -> {BeastStatus:F2} ({reason})");
            Changed?.Invoke(delta, BeastStatus);
        }

        /// <summary>Drops the accumulated pressure and subscribers (test isolation).</summary>
        public static void Clear()
        {
            BeastStatus = 0f;
            Changed = null;
        }
    }
}
