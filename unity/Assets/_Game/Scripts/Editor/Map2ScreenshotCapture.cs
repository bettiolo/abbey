using System;
using System.Collections.Generic;
using System.IO;
using Abbey.CameraRig;
using Abbey.Core;
using Abbey.Map2;
using Abbey.Nightmares;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Abbey.EditorTools
{
    public static class Map2ScreenshotCapture
    {
        public static readonly string[] ShotNames =
        {
            "map2_grove_day.png",
            "map2_false_bell_night.png",
        };

        static string OutputDir => Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", "Build", "screenshots"));

        public static string[] CaptureForGate()
        {
            Map2SceneBuilder.BuildMap2Scene();
            var rig = UnityEngine.Object.FindFirstObjectByType<IsoCameraController>();
            if (rig == null) throw new InvalidOperationException("Map 2 scene has no isometric camera.");

            rig.TargetCamera.orthographic = true;
            rig.TargetCamera.orthographicSize = 17f;
            rig.FocusOn(new Vector3(4f, 0f, 5f));
            Directory.CreateDirectory(OutputDir);

            ScreenshotCapture.ApplyLightingForPhase(DayPhase.Day);
            ScreenshotCapture.CaptureCameraTo(rig.TargetCamera, OutputDir, ShotNames[0]);

            var stag = StagCovenantSystem.Instance;
            for (int i = 0; i < 4; i++) stag?.RecordWorldChoice("old_growth_cutting");
            ThreatSourceSystem.Instance?.RecomputeFromLog();
            var guidance = FalseGuidanceSystem.Instance;
            guidance?.RecordNightmareSpawn(NightmareType.BellMimic,
                new Vector3(4f, 0f, 8f), Vector3.zero);

            // Screenshot-only presentation marker for the projected false light.
            // The runtime authority remains FalseGuidanceSystem/LightSource; this
            // disposable marker makes the misdirection beat legible in a still frame.
            if (guidance != null)
            {
                var falseLight = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                falseLight.name = "FalseLightProof";
                falseLight.transform.position = guidance.LastFalseLightTarget + Vector3.up * 1.2f;
                falseLight.transform.localScale = Vector3.one * 1.4f;
                var material = new Material(Shader.Find("Standard"));
                var cold = new Color(0.35f, 0.55f, 1f);
                material.color = cold;
                if (material.HasProperty("_EmissionColor"))
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", cold * 2.5f);
                }
                falseLight.GetComponent<Renderer>().sharedMaterial = material;
                var point = falseLight.AddComponent<UnityEngine.Light>();
                point.type = LightType.Point;
                point.color = cold;
                point.range = 14f;
                point.intensity = 2.2f;
            }

            ScreenshotCapture.ApplyLightingForPhase(DayPhase.Night);
            ScreenshotCapture.CaptureCameraTo(rig.TargetCamera, OutputDir, ShotNames[1]);

            if (File.Exists(Path.GetFullPath(Path.Combine(Application.dataPath, "..", Map2SceneBuilder.ScenePath))))
                EditorSceneManager.OpenScene(Map2SceneBuilder.ScenePath, OpenSceneMode.Single);
            return (string[])ShotNames.Clone();
        }
    }
}
