// Naija Streets (Unity) - the one job-slot registry (rule JS-7). Wraps the pure
// JobSlotBook. One shared instance per play session; M5 swaps in the network
// sync with UseSync() before any slot registers.

using NaijaLife.Jobs;
using UnityEngine;

namespace NaijaLife.JobsGlue
{
    public sealed class JobSlotRegistry : ScriptableObject
    {
        static JobSlotRegistry _shared;
        static IJobSlotSync _pendingSync;

        public JobSlotBook Book { get; private set; }

        /// <summary>The registry every JobSlot and PlayerJobs uses.</summary>
        public static JobSlotRegistry Shared
        {
            get
            {
                if (_shared == null)
                {
                    _shared = CreateInstance<JobSlotRegistry>();
                    _shared.hideFlags = HideFlags.DontSave;
                    _shared.Book = new JobSlotBook(_pendingSync);
                }
                return _shared;
            }
        }

        /// <summary>Call before the first slot registers (M5 network sync).</summary>
        public static void UseSync(IJobSlotSync sync)
        {
            if (_shared != null && _shared.Book.Count > 0)
            {
                Debug.LogError("[Jobs] UseSync called after slots registered; ignored.");
                return;
            }
            _pendingSync = sync;
            if (_shared != null) _shared.Book = new JobSlotBook(sync);
        }

        // Editor: fast enter-play-mode may keep statics; start clean each play.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _shared = null; _pendingSync = null; }
    }
}
