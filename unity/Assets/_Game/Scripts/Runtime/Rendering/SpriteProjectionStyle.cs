using Abbey.Core;
using UnityEngine;

namespace Abbey.Rendering
{
    /// <summary>Data-owned sprite depth and phase-grade settings.</summary>
    [CreateAssetMenu(fileName = "SpriteProjectionStyle", menuName = "Abbey/Rendering/Sprite Projection Style")]
    public sealed class SpriteProjectionStyle : ScriptableObject
    {
        [Min(0.01f)] public float depthSortingScale = 10f;
        [Min(1)] public int stableTieBreakRange = 4;
        public Color dayTint = Color.white;
        public Color duskTint = new Color(0.9f, 0.72f, 0.64f, 1f);
        public Color nightTint = new Color(0.42f, 0.52f, 0.72f, 1f);
        public Color dawnTint = new Color(0.82f, 0.78f, 0.82f, 1f);

        public Color TintFor(DayPhase phase)
        {
            switch (phase)
            {
                case DayPhase.Dusk: return duskTint;
                case DayPhase.Night: return nightTint;
                case DayPhase.Dawn: return dawnTint;
                default: return dayTint;
            }
        }

        static SpriteProjectionStyle cached;

        public static SpriteProjectionStyle LoadOrDefault()
        {
            if (cached == null)
            {
                cached = Resources.Load<SpriteProjectionStyle>("Rendering/SpriteProjectionStyle");
            }
            if (cached == null)
            {
                cached = CreateInstance<SpriteProjectionStyle>();
                cached.name = "SpriteProjectionStyle (defaults)";
                cached.hideFlags = HideFlags.HideAndDontSave;
            }
            return cached;
        }

        public static void ClearCache() => cached = null;
    }
}
