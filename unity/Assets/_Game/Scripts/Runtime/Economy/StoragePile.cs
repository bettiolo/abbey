using UnityEngine;

namespace Abbey.Economy
{
    /// <summary>
    /// A built Storage Pile: while enabled it raises the settlement's total storage
    /// capacity by <see cref="EconomyConfig.storagePileCapacity"/> (the ledger reads
    /// the value from config — no balance value lives here). [ExecuteAlways] so
    /// EditMode tests get OnEnable/OnDisable registration.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class StoragePile : MonoBehaviour
    {
        void OnEnable()
        {
            ResourceLedger.RegisterStorage(this);
        }

        void OnDisable()
        {
            ResourceLedger.UnregisterStorage(this);
        }
    }
}
