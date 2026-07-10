using System.Collections.Generic;
using UnityEngine;

namespace Abbey.Core
{
    /// <summary>
    /// A static, authored XZ footprint for scenery that must block kinematic walkers
    /// but is not a gameplay <see cref="Abbey.Buildings.Building"/>. Abbey walls and
    /// the ruined bell tower use this so direct input and AI steering share the same
    /// deterministic collision rule without a NavMesh or Rigidbody simulation.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class WorldObstacle : MonoBehaviour
    {
        static readonly List<WorldObstacle> Obstacles = new List<WorldObstacle>();

        [SerializeField] Vector2 localCenter;
        [SerializeField] Vector2 size = Vector2.one;

        public static IReadOnlyList<WorldObstacle> Active => Obstacles;

        public Rect Footprint => new Rect(
            transform.position.x + localCenter.x - size.x * 0.5f,
            transform.position.z + localCenter.y - size.y * 0.5f,
            size.x,
            size.y);

        public void Initialize(Rect worldFootprint)
        {
            localCenter = new Vector2(
                worldFootprint.center.x - transform.position.x,
                worldFootprint.center.y - transform.position.z);
            size = new Vector2(
                Mathf.Max(0f, worldFootprint.width),
                Mathf.Max(0f, worldFootprint.height));
        }

        public void Initialize(Bounds worldBounds)
        {
            Initialize(new Rect(
                worldBounds.min.x,
                worldBounds.min.z,
                worldBounds.size.x,
                worldBounds.size.z));
        }

        public static void ClearRegistry()
        {
            Obstacles.Clear();
        }

        void OnEnable()
        {
            if (!Obstacles.Contains(this))
            {
                Obstacles.Add(this);
            }
        }

        void OnDisable()
        {
            Obstacles.Remove(this);
        }

        void OnDrawGizmosSelected()
        {
            Rect rect = Footprint;
            Gizmos.color = new Color(1f, 0.55f, 0.12f, 0.8f);
            Gizmos.DrawWireCube(
                new Vector3(rect.center.x, transform.position.y + 0.2f, rect.center.y),
                new Vector3(rect.width, 0.4f, rect.height));
        }
    }
}
