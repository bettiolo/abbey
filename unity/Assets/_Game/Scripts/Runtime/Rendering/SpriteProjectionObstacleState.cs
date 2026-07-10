using Abbey.Core;
using UnityEngine;

namespace Abbey.Rendering
{
    /// <summary>Keeps the authored sprite footprint and original 3D footprint reversible.</summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(WorldObstacle))]
    public sealed class SpriteProjectionObstacleState : MonoBehaviour
    {
        [SerializeField] Rect legacyFootprint;
        [SerializeField] Rect spriteFootprint;
        WorldObstacle obstacle;

        public Rect LegacyFootprint => legacyFootprint;
        public Rect SpriteFootprint => spriteFootprint;

        public void Configure(WorldObstacle target, Rect legacy, Rect projected)
        {
            obstacle = target;
            legacyFootprint = legacy;
            spriteFootprint = projected;
        }

        public void Apply(bool projectionEnabled)
        {
            if (obstacle == null)
            {
                obstacle = GetComponent<WorldObstacle>();
            }
            obstacle?.Initialize(projectionEnabled ? spriteFootprint : legacyFootprint);
        }
    }
}
