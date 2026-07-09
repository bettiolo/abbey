using System;
using System.Collections.Generic;
using System.IO;
using Abbey.Beast;
using Abbey.CameraRig;
using Abbey.Debugging;
using Abbey.Hero;
using Abbey.Map2;
using Abbey.Session;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Abbey.EditorTools
{
    /// <summary>
    /// Generates the Abbey of Antlers scene from code.  It starts from the verified
    /// all-mechanics scene, removes Map-1-only beast/outcome objects, then lays out a
    /// compact dense forest with all nine authored landmarks and wires Map-2 systems.
    /// Runtime logic and tests never depend on the serialized scene.
    /// </summary>
    public static class Map2SceneBuilder
    {
        public const string ScenePath = "Assets/_Game/Scenes/Map2Prototype.unity";
        public static readonly Vector3 SacredGroveCenter = new Vector3(8f, 0f, 9f);

        static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();

        [MenuItem("Tools/Abbey/Build Map 2 Scene")]
        public static void BuildMap2Scene()
        {
            PrototypeSceneBuilder.BuildPrototypeScene();
            RemoveMap1OnlyObjects();
            BuildForestIdentity();
            WireMap2Systems();

            var scene = EditorSceneManager.GetActiveScene();
            string absolute = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ScenePath));
            Directory.CreateDirectory(Path.GetDirectoryName(absolute));
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException($"Could not save generated Map 2 scene to {ScenePath}.");
            ConfigureCampaignBuildSettings();
            Debug.Log($"[Abbey] Abbey of Antlers scene built and saved to {ScenePath}.");
        }

        [MenuItem("Tools/Abbey/Build Campaign Scenes (Start Map 1)")]
        public static void BuildCampaignScenes()
        {
            BuildMap2Scene(); // also regenerates Prototype01 first
            EditorSceneManager.OpenScene(PrototypeSceneBuilder.ScenePath, OpenSceneMode.Single);
            Debug.Log("[Abbey] Both campaign maps built; Prototype01 is ready to Play.");
        }

        static void ConfigureCampaignBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(PrototypeSceneBuilder.ScenePath, true),
                new EditorBuildSettingsScene(ScenePath, true),
            };
        }

        static void RemoveMap1OnlyObjects()
        {
            DestroyNamed("GameSession");
            DestroyNamed("FirstWhiteNightScenario");
            DestroyNamed("SpringShipScenario");
            DestroyNamed("SpringShipSite");
            string[] coastalVisuals =
            {
                "Ground", "Beach", "ForestFloor", "CampPlaza", "AbbeyPlaza",
                "Road_Beach_Camp", "Road_Camp_Abbey", "Road_Camp_Forest", "FieldPlot",
                "AbbeyHill", "BellTower", "AbbeyWall_A", "AbbeyWall_B", "HoundChain",
                "ShipwreckHull", "WreckCrate_A", "WreckCrate_B", "WreckBarrel",
                "SalvageSite_A", "SalvageSite_B", "Stream", "RockCluster",
            };
            for (int i = 0; i < coastalVisuals.Length; i++) DestroyNamed(coastalVisuals[i]);
            for (int i = 0; i < 5; i++) DestroyNamed($"ForestTree_{i}");

            var hound = UnityEngine.Object.FindFirstObjectByType<HoundController>();
            if (hound != null) UnityEngine.Object.DestroyImmediate(hound.gameObject);
            var evolution = UnityEngine.Object.FindFirstObjectByType<HoundEvolutionSystem>();
            if (evolution != null) UnityEngine.Object.DestroyImmediate(evolution);
            var flow = UnityEngine.Object.FindFirstObjectByType<CampaignFlowController>();
            if (flow != null) UnityEngine.Object.DestroyImmediate(flow);
        }

        static void BuildForestIdentity()
        {
            // A raised forest floor hides the coastal systems-test paint while retaining
            // all gameplay objects and authored positions beneath the new compact canopy.
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Map2_ForestFloor";
            floor.transform.position = new Vector3(0f, 0.026f, 0f);
            floor.transform.localScale = new Vector3(9.5f, 1f, 9.5f);
            Colorize(floor, "forest_floor", new Color(0.12f, 0.25f, 0.13f));

            Landmark("Map2_SacredGrove", SacredGroveCenter);
            PlaceGenerated("candle_shrine_t1", SacredGroveCenter, "GroveShrine_Map2",
                PrimitiveType.Cylinder, new Vector3(2f, 1f, 2f));

            Landmark("Map2_Orchard", new Vector3(-18f, 0f, 12f));
            PlaceGenerated("field_plot_t1", new Vector3(-18f, 0.04f, 12f), "Orchard_Map2",
                PrimitiveType.Cube, new Vector3(5f, 0.2f, 5f));

            Landmark("Map2_DeepForest", new Vector3(32f, 0f, 30f));

            var stream = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stream.name = "Map2_Stream";
            stream.transform.position = new Vector3(5f, 0.055f, 28f);
            stream.transform.localScale = new Vector3(55f, 0.08f, 3f);
            stream.transform.rotation = Quaternion.Euler(0f, -24f, 0f);
            Colorize(stream, "stream", new Color(0.16f, 0.34f, 0.48f));

            Landmark("Map2_CharcoalCamp", new Vector3(-28f, 0f, 24f));
            PlaceGenerated("charcoal_kiln_t1", new Vector3(-28f, 0f, 24f), "CharcoalCamp_Map2",
                PrimitiveType.Cylinder, new Vector3(2.5f, 1.6f, 2.5f));

            Landmark("Map2_DeerPaths", new Vector3(6f, 0f, 10f));
            var deerPathA = PlaceGenerated("dirt_road_segment", new Vector3(4f, 0.05f, 5f), "DeerPath_A",
                PrimitiveType.Cube, new Vector3(2f, 0.06f, 12f), Quaternion.Euler(0f, 38f, 0f));
            deerPathA.transform.localScale = new Vector3(1.5f, 1f, 3.5f);
            var deerPathB = PlaceGenerated("dirt_road_segment", new Vector3(-4f, 0.055f, 1f), "DeerPath_B",
                PrimitiveType.Cube, new Vector3(2f, 0.06f, 10f), Quaternion.Euler(0f, -55f, 0f));
            deerPathB.transform.localScale = new Vector3(1.2f, 1f, 2.5f);

            Landmark("Map2_StoneCircle", new Vector3(24f, 0f, -10f));
            for (int i = 0; i < 8; i++)
            {
                float angle = i / 8f * Mathf.PI * 2f;
                PlaceGenerated("rock_cluster_01",
                    new Vector3(24f + Mathf.Cos(angle) * 4f, 0f, -10f + Mathf.Sin(angle) * 4f),
                    $"StandingStone_{i:D2}", PrimitiveType.Cube, new Vector3(1f, 2.2f, 0.8f));
            }

            Landmark("Map2_HiddenGraves", new Vector3(30f, 0f, 5f));
            for (int i = 0; i < 5; i++)
            {
                PlaceGenerated("grave_marker", new Vector3(28f + i * 1.4f, 0f, 3f + (i % 2) * 1.8f),
                    $"HiddenGrave_{i:D2}", PrimitiveType.Cube, new Vector3(0.5f, 1f, 0.25f));
            }

            Landmark("Map2_CorruptedLoggingCamp", new Vector3(-32f, 0f, -6f));
            PlaceGenerated("woodcutter_t1", new Vector3(-32f, 0f, -6f), "CorruptedLoggingCamp_Map2",
                PrimitiveType.Cube, new Vector3(3f, 2f, 3f));

            Landmark("Map2_AbbeyOfAntlers", new Vector3(-10f, 0f, -9f));
            PlaceGenerated("abbey_cloister_t1", new Vector3(-10f, 0f, -9f), "AbbeyOfAntlers",
                PrimitiveType.Cube, new Vector3(8f, 2.5f, 6f));

            var flame = GameObject.Find("AbbeyFlame");
            if (flame != null) flame.transform.position = new Vector3(-8f, 0f, -7f);

            // Dense but readable ring: deterministic tree placement leaves the central
            // deer paths and landmark clearings open.
            for (int i = 0; i < 52; i++)
            {
                float angle = i * 2.3999632f;
                float radius = 20f + (i % 9) * 3.2f;
                var pos = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                string tree = (i % 3 == 0) ? "forest_tree_02" : "forest_tree_01";
                PlaceGenerated(tree, pos, $"Map2Tree_{i:D2}", PrimitiveType.Cylinder,
                    new Vector3(0.7f, 5f, 0.7f), Quaternion.Euler(0f, i * 37f, 0f));
            }
            for (int i = 0; i < 22; i++)
            {
                float angle = i / 22f * Mathf.PI * 2f + (i % 3) * 0.11f;
                float radius = 5.5f + (i % 4) * 2.1f;
                var pos = SacredGroveCenter +
                          new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                string tree = (i % 2 == 0) ? "forest_tree_02" : "forest_tree_01";
                PlaceGenerated(tree, pos, $"GroveTree_{i:D2}", PrimitiveType.Cylinder,
                    new Vector3(0.7f, 5f, 0.7f), Quaternion.Euler(0f, i * 29f, 0f));
            }

            var stag = PlaceGenerated("stag_beneath_abbey_lowpoly",
                SacredGroveCenter + new Vector3(2.6f, 0f, 1.8f), "StagBeneathAbbey",
                PrimitiveType.Capsule, new Vector3(1.3f, 2.2f, 1.3f), Quaternion.Euler(0f, 210f, 0f));
            stag.AddComponent<StagCovenantSystem>();
        }

        static void WireMap2Systems()
        {
            var mode = new GameObject("Map2Mode");
            var carry = mode.AddComponent<CampaignCarryoverSystem>();
            var scenario = mode.AddComponent<Map2Scenario>();
            scenario.bellkeeper = UnityEngine.Object.FindFirstObjectByType<BellkeeperController>();
            scenario.stag = UnityEngine.Object.FindFirstObjectByType<StagCovenantSystem>();
            mode.AddComponent<Map2DebugPanel>();
            carry.ApplyToWorld();

            var camera = UnityEngine.Object.FindFirstObjectByType<IsoCameraController>();
            if (camera != null)
            {
                camera.TargetCamera.orthographicSize = 24f;
                camera.FocusOn(new Vector3(4f, 0f, 5f));
            }
        }

        static void Landmark(string name, Vector3 position)
        {
            var root = new GameObject(name);
            root.transform.position = position;
        }

        static GameObject PlaceGenerated(
            string id, Vector3 position, string name, PrimitiveType fallback,
            Vector3 scale, Quaternion? rotation = null)
        {
            GameObject go = null;
            string path = $"{PrototypeSceneBuilder.GeneratedAssetFolder}/{id}.glb";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null) go = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (go == null)
            {
                go = GameObject.CreatePrimitive(fallback);
                go.transform.localScale = scale;
                if (fallback != PrimitiveType.Plane) go.transform.position = position + Vector3.up * scale.y * 0.5f;
                Colorize(go, "fallback", new Color(0.28f, 0.22f, 0.13f));
            }
            else
            {
                go.transform.position = position;
                PrototypeSceneBuilder.NormalizeImportedMaterials(go, id);
            }
            go.name = name;
            go.transform.rotation = rotation ?? Quaternion.identity;
            return go;
        }

        static void Colorize(GameObject go, string key, Color color)
        {
            var renderer = go != null ? go.GetComponent<Renderer>() : null;
            if (renderer == null) return;
            if (!Materials.TryGetValue(key, out var material) || material == null)
            {
                var shader = Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Diffuse");
                material = new Material(shader) { name = $"Map2_{key}", color = color };
                Materials[key] = material;
            }
            renderer.sharedMaterial = material;
        }

        static void DestroyNamed(string name)
        {
            var go = GameObject.Find(name);
            if (go != null) UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
