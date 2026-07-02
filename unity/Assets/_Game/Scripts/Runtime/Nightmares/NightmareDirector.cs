using System.Collections.Generic;
using Abbey.Core;
using Abbey.Light;
using Abbey.Villagers;
using UnityEngine;

namespace Abbey.Nightmares
{
    /// <summary>
    /// Minimal 0.1 night director. On PhaseChanged(Night) it spawns
    /// config.firstNightMonsterCount pale hounds at deterministic dark points on a
    /// ring beyond the map center (its own transform), raising MonsterSpawned for
    /// each. On Dawn it despawns everything and writes a NightSummary record —
    /// villagers dead/injured/missing, monsters killed, whether the hound helped —
    /// into the shared <see cref="GameEventLog"/> (one log, many consumers).
    /// </summary>
    [DisallowMultipleComponent]
    public class NightmareDirector : MonoBehaviour
    {
        [Tooltip("autoTick value handed to every spawned monster (tests set false).")]
        public bool monstersAutoTick = true;

        PrototypeConfig _config;
        readonly List<MonsterController> _spawned = new List<MonsterController>();
        int _nightStartLogIndex;
        int _nightNumber;

        public IReadOnlyList<MonsterController> SpawnedMonsters => _spawned;

        public PrototypeConfig Config
        {
            get
            {
                if (_config == null)
                {
                    _config = PrototypeConfig.LoadOrDefault();
                }
                return _config;
            }
            set { _config = value; }
        }

        void OnEnable()
        {
            EventBus.PhaseChanged += OnPhaseChanged;
        }

        void OnDisable()
        {
            EventBus.PhaseChanged -= OnPhaseChanged;
        }

        void OnPhaseChanged(DayPhase phase)
        {
            if (phase == DayPhase.Night)
            {
                BeginNight();
            }
            else if (phase == DayPhase.Dawn)
            {
                EndNight();
            }
        }

        /// <summary>Spawns the night's monsters. Public so tests/debug tools can force it.</summary>
        public void BeginNight()
        {
            var cfg = Config;
            _nightNumber++;
            _nightStartLogIndex = GameEventLog.Count;
            GameEventLog.Append("night_begins", $"night={_nightNumber}");

            for (int i = 0; i < cfg.firstNightMonsterCount; i++)
            {
                Vector3? point = FindDarkSpawnPoint(
                    transform.position,
                    cfg.monsterSpawnMinRadius,
                    cfg.monsterSpawnMaxRadius,
                    cfg.simulationSeed + _nightNumber * 977 + i * 131,
                    cfg.monsterSpawnAttempts);
                if (!point.HasValue)
                {
                    GameEventLog.Append("monster_spawn_failed", $"night={_nightNumber} index={i}");
                    continue;
                }

                var go = new GameObject($"PaleHound_{_nightNumber}_{i}");
                go.transform.position = point.Value;
                var monster = go.AddComponent<MonsterController>();
                monster.Config = cfg;
                monster.autoTick = monstersAutoTick;
                _spawned.Add(monster);
                EventBus.RaiseMonsterSpawned(go);
            }
        }

        /// <summary>Cleans up the night and writes the summary. Public for tests/debug tools.</summary>
        public void EndNight()
        {
            int dead = 0, injured = 0, missing = 0;
            var villagers = DuskRecallSystem.Villagers;
            for (int i = 0; i < villagers.Count; i++)
            {
                var v = villagers[i];
                if (v == null)
                {
                    continue;
                }
                switch (v.State)
                {
                    case VillagerState.Dead: dead++; break;
                    case VillagerState.Injured: injured++; break;
                    case VillagerState.Missing: missing++; break;
                }
            }

            int monstersKilled = 0;
            for (int i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i] == null || !_spawned[i].IsAlive)
                {
                    monstersKilled++;
                }
            }

            bool houndHelped = HoundHelpedSince(_nightStartLogIndex);

            GameEventLog.Append("NightSummary",
                $"night={_nightNumber} villagersDead={dead} villagersInjured={injured} " +
                $"villagersMissing={missing} monstersKilled={monstersKilled} houndHelped={houndHelped}");

            for (int i = 0; i < _spawned.Count; i++)
            {
                var monster = _spawned[i];
                if (monster == null)
                {
                    continue;
                }
                GameEventLog.Append("monster_despawned", monster.name);
                if (Application.isPlaying)
                {
                    Destroy(monster.gameObject);
                }
                else
                {
                    DestroyImmediate(monster.gameObject);
                }
            }
            _spawned.Clear();
        }

        static bool HoundHelpedSince(int logIndex)
        {
            var records = GameEventLog.Records;
            for (int i = logIndex; i < records.Count; i++)
            {
                string type = records[i].Type;
                if (type == "hound_engaged_monster"
                    || type == "hound_attacked_monster"
                    || type == "hound_killed_monster")
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Deterministically samples ring points (minRadius..maxRadius from center)
        /// until one classifies Dark — monsters are born outside all light. Returns
        /// null when every attempt landed in light. Static so EditMode tests can
        /// exercise spawn-point selection without a director instance.
        /// </summary>
        public static Vector3? FindDarkSpawnPoint(
            Vector3 center, float minRadius, float maxRadius, int seed, int attempts)
        {
            var rng = new System.Random(seed);
            float max = Mathf.Max(minRadius, maxRadius);
            for (int i = 0; i < attempts; i++)
            {
                float angle = (float)(rng.NextDouble() * Mathf.PI * 2.0);
                float radius = Mathf.Lerp(minRadius, max, (float)rng.NextDouble());
                var candidate = center + new Vector3(
                    Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                if (DarknessEvaluator.Classify(candidate) == LightZone.Dark)
                {
                    return candidate;
                }
            }
            return null;
        }
    }
}
