using Abbey.Core;
using Abbey.Light;
using Abbey.World;
using Abbey.UI;
using UnityEngine;

namespace Abbey.Debugging
{
    /// <summary>
    /// Debug overlay for the seasonal calendar + weather (AGENTS.md "debug overlays
    /// for every hidden system"). Toggled with <see cref="toggleKey"/> (F11, so it
    /// coexists with the other debug panels and the player HUD/minimap on F7/F8). Shows the current season, day-of-year / in-season day,
    /// the night-length multiplier, weather, moon phase, the live light/bell
    /// effectiveness multipliers, the White Night flag and the next omen day.
    /// Display-only: nothing here holds or tunes a balance value.
    /// </summary>
    [DisallowMultipleComponent]
    public class SeasonDebugPanel : MonoBehaviour
    {
        [Tooltip("Key that shows/hides the panel.")]
        public KeyCode toggleKey = KeyCode.F11;

        [Tooltip("Start with the panel visible.")]
        public bool visible;

        GUIStyle _labelStyle;
        GUIStyle _headerStyle;

        void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                visible = !visible;
            }
        }

        void OnGUI()
        {
            float width = 320f;
            float x = Screen.width - width - 8f;
            float y = 576f; // below the economy panel's stack on the right edge
            if (!visible)
            {
                HudHintDock.Draw(HudHintSlot.Season, $"[{toggleKey}] season panel");
                return;
            }

            EnsureStyles();

            GUILayout.BeginArea(new Rect(x, y, width, Mathf.Min(Screen.height - y - 8f, 220f)),
                GUI.skin.box);

            DrawSeason();
            DrawWeather();

            GUILayout.EndArea();
        }

        void EnsureStyles()
        {
            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, richText = false };
            }
            if (_headerStyle == null)
            {
                _headerStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13,
                    fontStyle = FontStyle.Bold
                };
            }
        }

        void Header(string text)
        {
            GUILayout.Space(4f);
            GUILayout.Label(text, _headerStyle);
        }

        void Line(string text)
        {
            GUILayout.Label(text, _labelStyle);
        }

        void DrawSeason()
        {
            var season = SeasonSystem.Instance;
            Header($"Season   [{toggleKey}] hide");
            if (season == null)
            {
                Line("no SeasonSystem in scene");
                return;
            }
            Line($"{season.CurrentSeason}  year {season.YearNumber}  " +
                 $"day {season.DayInSeason}/{season.Config.daysPerSeason} in season");
            Line($"day-of-year={season.DayOfYear}  " +
                 $"night x{season.NightLengthMultiplier:F2}" +
                 (GameClock.Instance != null
                     ? $"  ({GameClock.Instance.GetPhaseDuration(DayPhase.Night):F0}s night)"
                     : ""));
        }

        void DrawWeather()
        {
            var weather = WeatherSystem.Instance;
            Header("Weather / moon");
            if (weather == null)
            {
                Line("no WeatherSystem in scene");
                return;
            }
            Line($"{weather.CurrentWeather}  moon={weather.CurrentMoonPhase}" +
                 (weather.IsWhiteNight ? "  *WHITE NIGHT*" : ""));
            Line($"light x{weather.LightEffectivenessMultiplier:F2} " +
                 $"(global {DarknessEvaluator.LightEffectivenessMultiplier:F2})  " +
                 $"bell x{weather.BellReliabilityMultiplier:F2}");
            int? next = weather.NextWhiteNightDay();
            Line(next.HasValue ? $"next omen: day {next.Value}" : "next omen: none this year");
        }
    }
}
