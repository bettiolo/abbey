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

        public string AssetId => assetId;
        public string Role => role;
        public string StableId => stableId;

        public void Configure(string newAssetId, string newRole, string newStableId)
        {
            assetId = newAssetId;
            role = newRole;
            stableId = newStableId;
        }
    }
}
