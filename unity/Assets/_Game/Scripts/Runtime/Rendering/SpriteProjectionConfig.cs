using UnityEngine;

namespace Abbey.Rendering
{
    /// <summary>Persistent, data-owned switch for the reversible presentation candidate.</summary>
    [CreateAssetMenu(fileName = "SpriteProjectionConfig", menuName = "Abbey/Rendering/Sprite Projection Config")]
    public sealed class SpriteProjectionConfig : ScriptableObject
    {
        public const string AssetPath = "Assets/_Game/Settings/Rendering/SpriteProjectionConfig.asset";

        [Tooltip("Disable and regenerate either map to restore the complete 3D presentation.")]
        public bool projectionEnabled = true;

        public SpriteProjectionStyle style;
    }
}
