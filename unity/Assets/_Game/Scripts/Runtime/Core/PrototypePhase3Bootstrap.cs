using Abbey.Session;
using UnityEngine;

namespace Abbey.Core
{
    /// <summary>
    /// Scene-local switch that makes the generated prototype scene run the full
    /// Phase 3 Map 1 loop while preserving coded defaults for isolated tests.
    /// </summary>
    [DisallowMultipleComponent]
    public class PrototypePhase3Bootstrap : MonoBehaviour
    {
        [Tooltip("Run the Phase 3 escalating-night director branch.")]
        public bool enablePhase3Nights = true;

        [Tooltip("Run the four-chapter spring-ship campaign instead of ending at the Phase 2 White Night.")]
        public bool enablePhase3Campaign = true;

        void Awake()
        {
            Apply();
        }

        /// <summary>Applies this scene's Phase 3 mode to the shared runtime config objects.</summary>
        public void Apply()
        {
            ApplyTo(PrototypeConfig.LoadOrDefault(), GameSessionConfig.LoadOrDefault(),
                enablePhase3Nights, enablePhase3Campaign);
        }

        public static void ApplyTo(
            PrototypeConfig prototype,
            GameSessionConfig session,
            bool enableNights,
            bool enableCampaign)
        {
            if (prototype != null)
            {
                prototype.phase3NightsEnabled = enableNights;
            }
            if (session != null)
            {
                session.phase3CampaignEnabled = enableCampaign;
            }
        }
    }
}
