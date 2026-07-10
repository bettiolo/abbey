using System;
using System.IO;
using Abbey.CameraRig;
using Abbey.Core;
using Abbey.Nightmares;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

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
        public static readonly string[] CanonicalShotNames =
        {
            "day_camp.png",
            "dusk_recall.png",
            "night_attack.png",
            "morning_after.png",
        };

        static string OutputDir => Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", "Screenshots"));

        /// <summary>
        /// The four canonical aesthetic-gate proofs land here (gitignored
        /// unity/Build/). CI reads them from this fixed path.
        /// </summary>
        static string CanonicalOutputDir => Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", "Build", "screenshots"));

        [MenuItem("Tools/Abbey/Capture Screenshots (Day-Dusk-Night)")]
        public static void CaptureFromMenu()
        {
            CaptureAll();
            Debug.Log($"[Abbey] Screenshots written to {OutputDir}");
        }

        [MenuItem("Tools/Abbey/Capture Canonical Shots")]
        public static void CaptureCanonicalFromMenu()
        {
            CaptureCanonicalShots();
            Debug.Log($"[Abbey] Canonical shots written to {CanonicalOutputDir}");
        }

        /// <summary>
        /// CLI entry for the four canonical aesthetic-gate proofs (day_camp,
        /// dusk_recall, night_attack, morning_after):
        ///
        ///   Unity -batchmode -projectPath unity \
        ///     -executeMethod Abbey.EditorTools.ScreenshotCapture.CaptureCanonicalFromCLI \
        ///     -quit -logFile -
        ///
        /// Needs a GPU context (run WITHOUT -nographics). Exits nonzero on failure so
        /// GameCI can gate on it. Writes to unity/Build/screenshots/&lt;name&gt;.png.
        /// </summary>
        public static void CaptureCanonicalFromCLI()
        {
            int exitCode = 0;
            try
            {
                CaptureCanonicalShotsForGate();
            }
            catch (Exception e)
            {
                Debug.LogError($"[Abbey] Canonical shot capture failed: {e}");
                exitCode = 1;
            }

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(exitCode);
            }
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

        /// <summary>
        /// Public wrapper for the combined Unity gate. Returns the deterministic
        /// file names expected under unity/Build/screenshots/.
        /// </summary>
        public static string[] CaptureCanonicalShotsForGate()
        {
            CaptureCanonicalShots();
            return (string[])CanonicalShotNames.Clone();
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

        /// <summary>
        /// The four canonical aesthetic-gate proofs (VERTICAL_SLICE_SPEC §3 beats):
        /// day_camp (bucolic day on the meadow camp), dusk_recall (colour drains,
        /// the bell rings), night_attack (near-dark, monsters at the edge of the
        /// light), morning_after (soft dawn over the settlement). Builds/opens the
        /// Prototype scene, drives the clock to each beat, adjusts sun/ambient
        /// (presentation only), renders the locked iso camera and writes each PNG to
        /// <see cref="CanonicalOutputDir"/>. The scene file on disk is left untouched.
        /// </summary>
        static void CaptureCanonicalShots()
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
            rig.transform.rotation = Quaternion.Euler(
                IsoCameraController.Pitch, IsoCameraController.Yaw, 0f);
            rig.TargetCamera.orthographic = true;
            rig.TargetCamera.orthographicSize = clock.Config.cameraMaxOrthoSize;
            rig.FocusOn(Vector3.zero);

            Directory.CreateDirectory(CanonicalOutputDir);

            // day_camp.
            ApplyPhaseLighting(DayPhase.Day);
            CaptureTo(rig.TargetCamera, CanonicalOutputDir, CanonicalShotNames[0]);

            // dusk_recall: cross the Day boundary (raises PhaseChanged -> dusk recall).
            clock.Tick(clock.GetPhaseDuration(DayPhase.Day) - clock.TimeInPhase + 0.01f);
            ApplyPhaseLighting(DayPhase.Dusk);
            CaptureTo(rig.TargetCamera, CanonicalOutputDir, CanonicalShotNames[1]);

            // night_attack: cross the Dusk boundary and force the night's monsters
            // into frame (the director is not event-subscribed in edit mode).
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
            CaptureTo(rig.TargetCamera, CanonicalOutputDir, CanonicalShotNames[2]);

            // morning_after: cross the Night boundary into Dawn.
            clock.Tick(clock.GetPhaseDuration(DayPhase.Night) + 0.01f);
            ApplyPhaseLighting(DayPhase.Dawn);
            CaptureTo(rig.TargetCamera, CanonicalOutputDir, CanonicalShotNames[3]);

            // Leave the saved scene exactly as the bootstrapper wrote it.
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
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientIntensity = 1f;

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
                case DayPhase.Dawn:
                    // Soft, cool-warm first light: brighter than night, gentler than day.
                    SetSun(sun, 8f, 0.5f, new Color(1f, 0.78f, 0.62f));
                    RenderSettings.ambientLight = new Color(0.32f, 0.34f, 0.4f);
                    break;
                default: // Night
                    SetSun(sun, 35f, 0.08f, new Color(0.5f, 0.6f, 0.9f));
                    RenderSettings.ambientLight = new Color(0.08f, 0.1f, 0.16f);
                    break;
            }
        }

        public static void ApplyLightingForPhase(DayPhase phase) => ApplyPhaseLighting(phase);

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
            CaptureTo(camera, OutputDir, fileName);
        }

        static void CaptureTo(Camera camera, string dir, string fileName)
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

                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, fileName);
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

        public static void CaptureCameraTo(Camera camera, string dir, string fileName) =>
            CaptureTo(camera, dir, fileName);
    }
}
