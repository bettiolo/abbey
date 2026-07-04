using UnityEngine;

namespace Abbey.Economy
{
    /// <summary>
    /// Play-mode economy bootstrap for the generated Prototype scene (P2-10 scene
    /// integration). On Start it grants the wreck-crate starting stock
    /// (VERTICAL_SLICE_SPEC §4) exactly once, after every <see cref="StoragePile"/>
    /// has raised the ledger's capacity in its own OnEnable. Balance values live in
    /// <see cref="EconomyConfig"/> (ResourceLedger.GrantStartingStock reads them) —
    /// this component owns only the timing.
    ///
    /// It exists because the editor scene bootstrap cannot grant the stock itself:
    /// <see cref="ResourceLedger"/> is a static that resets on entering play mode,
    /// so a grant made at scene-build time would be gone before the run begins. This
    /// is the runtime hook the scene bootstrap wires in its place. Idempotent within
    /// a run; deterministic (no RNG).
    /// </summary>
    [DisallowMultipleComponent]
    public class SettlementEconomyBootstrap : MonoBehaviour
    {
        [Tooltip("Grant the wreck-crate starting stock on Start (VERTICAL_SLICE_SPEC §4).")]
        public bool grantStartingStock = true;

        bool _granted;

        void Start()
        {
            if (!Application.isPlaying || _granted || !grantStartingStock)
            {
                return;
            }
            _granted = true;
            ResourceLedger.GrantStartingStock();
        }
    }
}
