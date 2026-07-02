using System;
using System.IO;
using Abbey.CameraRig;
using Abbey.Core;
using Abbey.Nightmares;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Abbey.EditorTools
{
    /// <summary>
    /// Headless in-engine screenshot capture (the editor half of
    /// tools/capture_unity_screenshot.py). Builds/opens the Prototype01 scene,
    /// drives the GameClock manually to Day, Dusk and Night, adjusts sun/ambient
    /// per phase (presentation only), renders the locked iso camera into a
    /// RenderTexture and writes PNGs to unity/Screenshots/. Works in batchmode
    /// WITHOUT -nographics (rendering needs a GPU context); CLI entry exits with
    /// a nonzero code on failure so CI can gate on it.
    /// </summary>
    public static class ScreenshotCapture
    {
        const int Width = 1920;
        const int Height = 1080;

        static string OutputDir => Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", "Screenshots"));

        [MenuItem("Tools/Abbey/Capture Screenshots (Day-Dusk-Night)")]
        public static void CaptureFromMenu()
        {
            CaptureAll();
            Debug.Log($"[Abbey] Screenshots written to {OutputDir}");
        }

        /// <summary>
        /// CLI entry: Unity -batchmode -projectPath unity
        /// -executeMethod Abbey.EditorTools.ScreenshotCapture.CaptureFromCLI -quit -logFile -
        /// (also reachable as Abbey.Editor.ScreenshotCapture.CaptureFromCLI via the
        /// legacy shim, which is what tools/capture_unity_screenshot.py invokes).
        /// </summary>
        public static void CaptureFromCLI()
        {
            int exitCode = 0;
            try
            {
                CaptureAll();
            }
            catch (Exception e)
            {
                Debug.LogError($"[Abbey] Screenshot capture failed: {e}");
                exitCode = 1;
            }

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(exitCode);
            }
        }

        static void CaptureAll()
        {
            OpenOrBuildScene();

            var clock = UnityEngine.Object.FindFirstObjectByType<GameClock>();
            if (clock == null)
            {
                throw new InvalidOperationException("Prototype scene has no GameClock.");
            }
            clock.autoTick = false;

            var rig = UnityEngine.Object.FindFirstObjectByType<IsoCameraController>();
            if (rig == null)
            {
                throw new InvalidOperationException("Prototype scene has no IsoCameraController.");
            }
            // Frame the camp; in edit mode LateUpdate never runs, so position explicitly.
            rig.transform.rotation = Quaternion.Euler(
                IsoCameraController.Pitch, IsoCameraController.Yaw, 0f);
            rig.TargetCamera.orthographic = true;
            rig.TargetCamera.orthographicSize = clock.Config.cameraMaxOrthoSize;
            rig.FocusOn(Vector3.zero);

            Directory.CreateDirectory(OutputDir);

            // Day.
            ApplyPhaseLighting(DayPhase.Day);
            Capture(rig.TargetCamera, "prototype01_day.png");

            // Dusk: cross the Day boundary (raises PhaseChanged -> dusk recall).
            clock.Tick(clock.GetPhaseDuration(DayPhase.Day) - clock.TimeInPhase + 0.01f);
            ApplyPhaseLighting(DayPhase.Dusk);
            Capture(rig.TargetCamera, "prototype01_dusk.png");

            // Night: cross the Dusk boundary; make sure monsters are in frame even
            // though the director (no [ExecuteAlways]) is not event-subscribed in
            // edit mode.
            clock.Tick(clock.GetPhaseDuration(DayPhase.Dusk) + 0.01f);
            if (clock.Phase != DayPhase.Night)
            {
                throw new InvalidOperationException(
                    $"Clock should be at Night but is at {clock.Phase}.");
            }
            var director = UnityEngine.Object.FindFirstObjectByType<NightmareDirector>();
            if (director != null && director.SpawnedMonsters.Count == 0)
            {
                director.monstersAutoTick = false;
                director.BeginNight();
            }
            ApplyPhaseLighting(DayPhase.Night);
            Capture(rig.TargetCamera, "prototype01_night.png");

            // Discard the ticked state: reload the saved scene so the asset on
            // disk stays exactly as the bootstrapper wrote it.
            if (File.Exists(Path.GetFullPath(Path.Combine(
                    Application.dataPath, "..", PrototypeSceneBuilder.ScenePath))))
            {
                EditorSceneManager.OpenScene(PrototypeSceneBuilder.ScenePath, OpenSceneMode.Single);
            }
        }

        static void OpenOrBuildScene()
        {
            string absolute = Path.GetFullPath(Path.Combine(
                Application.dataPath, "..", PrototypeSceneBuilder.ScenePath));
            if (File.Exists(absolute))
            {
                EditorSceneManager.OpenScene(PrototypeSceneBuilder.ScenePath, OpenSceneMode.Single);
            }
            else
            {
                PrototypeSceneBuilder.BuildPrototypeScene();
            }
        }

        /// <summary>Presentation-only sun/ambient look per phase (not balance).</summary>
        static void ApplyPhaseLighting(DayPhase phase)
        {
            var sunGO = GameObject.Find("Sun");
            var sun = sunGO != null ? sunGO.GetComponent<UnityEngine.Light>() : null;

            switch (phase)
            {
                case DayPhase.Day:
                    SetSun(sun, 50f, 1f, new Color(1f, 0.96f, 0.88f));
                    RenderSettings.ambientLight = new Color(0.55f, 0.57f, 0.6f);
                    break;
                case DayPhase.Dusk:
                    SetSun(sun, 12f, 0.55f, new Color(1f, 0.62f, 0.35f));
                    RenderSettings.ambientLight = new Color(0.35f, 0.28f, 0.3f);
                    break;
                default: // Night / Dawn
                    SetSun(sun, 35f, 0.08f, new Color(0.5f, 0.6f, 0.9f));
                    RenderSettings.ambientLight = new Color(0.08f, 0.1f, 0.16f);
                    break;
            }
        }

        static void SetSun(UnityEngine.Light sun, float pitch, float intensity, Color color)
        {
            if (sun == null)
            {
                return;
            }
            sun.transform.rotation = Quaternion.Euler(pitch, IsoCameraController.Yaw - 30f, 0f);
            sun.intensity = intensity;
            sun.color = color;
        }

        static void Capture(Camera camera, string fileName)
        {
            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            Texture2D tex = null;
            try
            {
                camera.targetTexture = rt;
                camera.Render();

                RenderTexture.active = rt;
                tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                tex.Apply();

                string path = Path.Combine(OutputDir, fileName);
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Debug.Log($"[Abbey] Captured {path}");
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                if (tex != null)
                {
                    UnityEngine.Object.DestroyImmediate(tex);
                }
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
            }
        }
    }
}
