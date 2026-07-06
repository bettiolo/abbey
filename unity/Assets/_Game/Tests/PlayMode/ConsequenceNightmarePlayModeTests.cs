using System.Collections;
using System.Collections.Generic;
using Abbey.Core;
using Abbey.Decrees;
using Abbey.Light;
using Abbey.Nightmares;
using Abbey.Villagers;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Abbey.Tests.PlayMode
{
    /// <summary>
    /// PlayMode coverage for P3-11 consequence nightmares end-to-end through the
    /// <see cref="NightmareDirector"/>, world built programmatically with manual ticks:
    /// a Phase 3 night with a Mass Graves grave tag in the log arms a Grave Crawler that
    /// spawns at the pressured crypt source; the same seed with no such tag spawns none.
    /// </summary>
    public class ConsequenceNightmarePlayModeTests
    {
        readonly List<GameObject> _spawned = new List<GameObject>();
        readonly List<ScriptableObject> _assets = new List<ScriptableObject>();
        PrototypeConfig _proto;
        ThreatConfig _threatCfg;

        [SetUp]
        public void SetUp()
        {
            ClearStatics();

            _proto = ScriptableObject.CreateInstance<PrototypeConfig>();
            _assets.Add(_proto);
            _proto.phase3NightsEnabled = true;
            _proto.edgeBandFraction = 0.3f;
            _proto.arrivalRadius = 0.3f;
            _proto.simulationSeed = 4242;
            _proto.monsterMaxHealth = 30f;
            _proto.monsterSpawnMinRadius = 8f;
            _proto.monsterSpawnMaxRadius = 16f;
            _proto.monsterSpawnAttempts = 48;
            _proto.nightDurationSeconds = 1f;

            _threatCfg = ScriptableObject.CreateInstance<ThreatConfig>();
            _assets.Add(_threatCfg);

            DarknessEvaluator.Config = _proto;
            DuskRecallSystem.Config = _proto;
        }

        [TearDown]
        public void TearDown()
        {
            var monsters = new List<MonsterController>(MonsterController.Active);
            foreach (var m in monsters)
            {
                if (m != null)
                {
                    Object.DestroyImmediate(m.gameObject);
                }
            }
            if (LawSystem.Instance != null)
            {
                Object.DestroyImmediate(LawSystem.Instance.gameObject);
            }
            if (ThreatSourceSystem.Instance != null)
            {
                Object.DestroyImmediate(ThreatSourceSystem.Instance.gameObject);
            }
            foreach (var go in _spawned)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }
            _spawned.Clear();
            foreach (var a in _assets)
            {
                if (a != null)
                {
                    Object.DestroyImmediate(a);
                }
            }
            _assets.Clear();
            ClearStatics();
        }

        static void ClearStatics()
        {
            EventBus.ResetAll();
            GameEventLog.Clear();
            DarknessEvaluator.Clear();
            DuskRecallSystem.Clear();
            MonsterController.ClearRegistry();
            NightmareDirector.ResetStaticEvents();
            Abbey.Buildings.AbbeyState.Clear();
            ThreatConfig.ClearCache();
            PrototypeConfig.ClearCache();
        }

        LawSystem MakeLaws()
        {
            var go = new GameObject("Laws");
            _spawned.Add(go);
            return go.AddComponent<LawSystem>();
        }

        ThreatSourceSystem MakeThreatWithCryptOnly(Vector3 cryptPos)
        {
            var go = new GameObject("ThreatSources");
            _spawned.Add(go);
            var sys = go.AddComponent<ThreatSourceSystem>();
            sys.Config = _threatCfg;
            sys.RegisterSource(ThreatSourceType.Crypt, cryptPos);
            return sys;
        }

        NightmareDirector MakeDirector()
        {
            var go = new GameObject("Director");
            _spawned.Add(go);
            go.transform.position = Vector3.zero;
            var d = go.AddComponent<NightmareDirector>();
            d.autoTick = false;
            d.monstersAutoTick = false;
            d.Config = _proto;
            d.ThreatCfg = _threatCfg;
            return d;
        }

        static bool AnyOfType(NightmareDirector dir, NightmareType type)
        {
            var monsters = dir.SpawnedMonsters;
            for (int i = 0; i < monsters.Count; i++)
            {
                if (monsters[i] != null && monsters[i].Type == type)
                {
                    return true;
                }
            }
            return false;
        }

        [UnityTest]
        public IEnumerator MassGraveTag_SummonsGraveCrawler_AtTheCrypt()
        {
            yield return null;

            MakeLaws(); // present ⇒ the director evaluates consequence triggers
            var cryptPos = new Vector3(30f, 0f, 30f);
            MakeThreatWithCryptOnly(cryptPos);

            // A mass grave was dug: the per-death grave tag sits in the log.
            GameEventLog.Append("villager_died", "V1");
            GameEventLog.Append("burial", "law=mass_graves_active deceased=V1 tag=grave_mass");

            var director = MakeDirector();
            director.BeginNight();

            Assert.IsTrue(AnyOfType(director, NightmareType.GraveCrawler),
                "the mass grave raises a Grave Crawler in the night's spawn set");

            // It came up at the only pressured source: the crypt.
            MonsterController crawler = null;
            foreach (var m in director.SpawnedMonsters)
            {
                if (m != null && m.Type == NightmareType.GraveCrawler)
                {
                    crawler = m;
                    break;
                }
            }
            Assert.IsNotNull(crawler);
            Assert.LessOrEqual(
                PlanarMotion.Distance(crawler.transform.position, cryptPos),
                _proto.monsterSpawnMinRadius + 1e-3f,
                "the Grave Crawler spawns beside the pressured crypt source");
            Assert.IsInstanceOf<ConsequenceMonsterController>(crawler);

            director.EndNight();
        }

        [UnityTest]
        public IEnumerator NoGraveTag_SameSeed_NoGraveCrawler()
        {
            yield return null;

            MakeLaws();
            MakeThreatWithCryptOnly(new Vector3(30f, 0f, 30f));

            // No mass grave, no death tags — the humane defaults hold.
            var director = MakeDirector();
            director.BeginNight();

            Assert.IsFalse(AnyOfType(director, NightmareType.GraveCrawler),
                "with no grave tag the Grave Crawler stays buried");

            director.EndNight();
        }
    }
}
