using UnityEngine;

namespace Abbey.Rendering
{
    /// <summary>Stable sprite identity carried by a gameplay or static-art root.</summary>
    [DisallowMultipleComponent]
    public sealed class SpriteRoleTag : MonoBehaviour
    {
        [SerializeField] string assetId;
        [SerializeField] string role;
        [SerializeField] string stableId;
        [SerializeField] int deterministicSortIndex;

        public string AssetId => assetId;
        public string Role => role;
        public string StableId => stableId;
        public int DeterministicSortIndex => deterministicSortIndex;

        public int StableSortKey
        {
            get
            {
                uint hash = 2166136261u;
                hash = Append(hash, stableId);
                hash = Append(hash, assetId);
                hash = Append(hash, role);
                return (int)(hash & 0x7fffffffu);
            }
        }

        public void Configure(string newAssetId, string newRole, string newStableId)
        {
            assetId = newAssetId;
            role = newRole;
            stableId = newStableId;
        }

        internal void SetDeterministicSortIndex(int index)
        {
            deterministicSortIndex = Mathf.Max(0, index);
        }

        static uint Append(uint hash, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return hash;
            }
            for (int i = 0; i < value.Length; i++)
            {
                hash ^= value[i];
                hash *= 16777619u;
            }
            return hash;
        }
    }
}
