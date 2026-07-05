using UnityEngine;

namespace Abbey.Nightmares
{
    /// <summary>
    /// The kind of problem only a dark-capable unit can solve tonight (ROADMAP Phase 3
    /// item 17: "every night also has one problem only a dark-capable unit can solve").
    /// </summary>
    public enum DarkObjectiveKind
    {
        /// <summary>A villager collapsed beyond the lanterns; a warrior must reach them.</summary>
        DownedVillager,

        /// <summary>A boundary lantern was breached and must be relit out in the dark.</summary>
        BreachedLantern,

        /// <summary>A monster nest is igniting outside all light and must be put out.</summary>
        MonsterNest
    }

    /// <summary>One generated dark objective: what it is and where (always in the Dark).</summary>
    public readonly struct DarkObjective
    {
        public readonly DarkObjectiveKind Kind;
        public readonly Vector3 Location;

        public DarkObjective(DarkObjectiveKind kind, Vector3 location)
        {
            Kind = kind;
            Location = location;
        }

        public string KindId
        {
            get
            {
                switch (Kind)
                {
                    case DarkObjectiveKind.DownedVillager: return "downed_villager";
                    case DarkObjectiveKind.BreachedLantern: return "breached_lantern";
                    default: return "monster_nest";
                }
            }
        }
    }

    /// <summary>
    /// Seeded generator for the nightly dark objective (P3-06). Pure and deterministic:
    /// given a seed base and the night index it derives the same kind + location every
    /// run, and the location is sampled through
    /// <see cref="NightmareDirector.FindDarkSpawnPoint"/> so it ALWAYS classifies Dark
    /// (outside every Safe light) at generation time — turtling inside the light can
    /// never solve it. No side effects; the lifecycle (marker, events, consequence)
    /// lives in <see cref="NightEscalationSystem"/>.
    /// </summary>
    public static class DarkObjectiveGenerator
    {
        static readonly DarkObjectiveKind[] Kinds =
        {
            DarkObjectiveKind.DownedVillager,
            DarkObjectiveKind.BreachedLantern,
            DarkObjectiveKind.MonsterNest
        };

        /// <summary>
        /// Deterministically generates tonight's objective. Returns false only when no
        /// dark point could be found within <paramref name="attempts"/> (everything is
        /// lit). The seed is derived from <paramref name="seedBase"/> and
        /// <paramref name="nightIndex"/> so the sequence is reproducible.
        /// </summary>
        public static bool TryGenerate(int seedBase, int nightIndex, Vector3 center,
            float minRadius, float maxRadius, int attempts, out DarkObjective objective)
        {
            objective = default;
            int seed = seedBase + nightIndex * 1013904223;
            var rng = new System.Random(seed);

            var kind = Kinds[rng.Next(Kinds.Length)];
            // Consume a value so the location seed varies from the kind draw, then let
            // FindDarkSpawnPoint (deterministic for a given seed) place it in the dark.
            int locationSeed = seed ^ rng.Next();
            Vector3? point = NightmareDirector.FindDarkSpawnPoint(
                center, minRadius, maxRadius, locationSeed, Mathf.Max(1, attempts));
            if (!point.HasValue)
            {
                return false;
            }

            objective = new DarkObjective(kind, point.Value);
            return true;
        }
    }
}
