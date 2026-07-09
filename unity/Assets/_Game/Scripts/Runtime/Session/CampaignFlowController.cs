using UnityEngine;
using UnityEngine.SceneManagement;

namespace Abbey.Session
{
    /// <summary>
    /// Bridges the two shipped prototype maps.  The Spring Ship saves its outcome;
    /// once that win fires, Return sails to Map2Prototype, where the carryover system
    /// loads the outcome and grants exactly one Bellkeeper trait.
    /// </summary>
    [DisallowMultipleComponent]
    public class CampaignFlowController : MonoBehaviour
    {
        public const string Map2SceneName = "Map2Prototype";

        public bool Map2Unlocked { get; private set; }

        void OnEnable()
        {
            SpringShipScenario.ShipSailed -= OnShipSailed;
            SpringShipScenario.ShipSailed += OnShipSailed;
        }

        void Start()
        {
            Map2Unlocked = CanUnlock(CampaignOutcome.Load());
        }

        void OnDisable() => SpringShipScenario.ShipSailed -= OnShipSailed;

        void OnShipSailed(CampaignOutcome outcome) => Map2Unlocked = CanUnlock(outcome);

        void Update()
        {
            if (Application.isPlaying && Map2Unlocked && Input.GetKeyDown(KeyCode.Return))
                LoadMap2();
        }

        void OnGUI()
        {
            if (!Application.isPlaying || !Map2Unlocked) return;
            var rect = new Rect(Screen.width * 0.5f - 190f, 18f, 380f, 42f);
            GUI.Box(rect, "The spring tide is ready — press Return to sail to the Abbey of Antlers");
        }

        public bool LoadMap2()
        {
            if (!Map2Unlocked || !Application.CanStreamedLevelBeLoaded(Map2SceneName)) return false;
            SceneManager.LoadScene(Map2SceneName, LoadSceneMode.Single);
            return true;
        }

        public static bool CanUnlock(CampaignOutcome outcome) =>
            outcome != null && outcome.Result == CampaignResult.ShipSailed;
    }
}
