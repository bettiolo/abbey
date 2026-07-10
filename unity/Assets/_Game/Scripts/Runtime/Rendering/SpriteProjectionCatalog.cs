using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abbey.Rendering
{
    /// <summary>One manifest-generated mapping from an Abbey identity to a sprite visual.</summary>
    [Serializable]
    public sealed class SpriteProjectionEntry
    {
        [Tooltip("Stable gameplay or generated-asset identity. Takes priority over role lookup.")]
        public string assetId;

        [Tooltip("Presentation role used by scene-authored objects when no asset id is available.")]
        public string role;

        public Sprite sprite;

        [Min(0.01f)] public float visualScale = 1f;
        public Vector3 anchorOffset;
        public int sortingOffset;

        [Tooltip("Optional authored XZ obstacle size; sprite bounds must never define gameplay collision.")]
        public Vector2 authoredFootprint;
    }

    /// <summary>
    /// Data-only sprite lookup. The deterministic importer owns the serialized entries;
    /// runtime callers resolve an explicit asset id first, then a role fallback.
    /// </summary>
    [CreateAssetMenu(fileName = "SpriteProjectionCatalog", menuName = "Abbey/Rendering/Sprite Projection Catalog")]
    public sealed class SpriteProjectionCatalog : ScriptableObject
    {
        public List<SpriteProjectionEntry> entries = new List<SpriteProjectionEntry>();

        public bool TryGet(string assetId, string role, out SpriteProjectionEntry entry)
        {
            entry = null;
            if (entries == null)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(assetId))
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    SpriteProjectionEntry candidate = entries[i];
                    if (candidate != null
                        && string.Equals(candidate.assetId, assetId, StringComparison.Ordinal))
                    {
                        entry = candidate;
                        return true;
                    }
                }
            }

            if (!string.IsNullOrEmpty(role))
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    SpriteProjectionEntry candidate = entries[i];
                    if (candidate != null
                        && string.Equals(candidate.role, role, StringComparison.Ordinal))
                    {
                        entry = candidate;
                        return true;
                    }
                }
            }

            return false;
        }

        public bool TryGet(SpriteRoleTag tag, out SpriteProjectionEntry entry)
        {
            if (tag == null)
            {
                entry = null;
                return false;
            }
            return TryGet(tag.AssetId, tag.Role, out entry);
        }
    }
}
