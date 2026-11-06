// Naija Streets (Unity) - the local player's side of the job-slot system
// (docs/specs/jobs-m4.md section 4). Offers "Take over from this conductor"
// near a free slot (shows at 2.5 m, hides past 3.0 m: HUD spec 6.3), and
// hands the job to ConductorShift. One job at a time (JS-4).

using NaijaLife.Jobs;
using NaijaLife.Player;
using UnityEngine;

namespace NaijaLife.JobsGlue
{
    [RequireComponent(typeof(PlayerNeeds))]
    public sealed class PlayerJobs : MonoBehaviour
    {
        public const string OfferId = "job.take";
        const float ShowDist = 2.5f, HideDist = 3.0f;

        public static PlayerJobs LocalPlayer { get; private set; }

        [Tooltip("Leave empty to use the JobDebugUi in the scene.")]
        public MonoBehaviour uiOverride;

        public IJobUi Ui { get; private set; }
        public PlayerNeeds Needs { get; private set; }
        public ConductorShift Conductor { get; private set; }

        string _offered;

        void Awake()
        {
            LocalPlayer = this;
            Needs = GetComponent<PlayerNeeds>();
            Conductor = GetComponent<ConductorShift>();
            if (!Conductor) Conductor = gameObject.AddComponent<ConductorShift>();
            Ui = uiOverride as IJobUi;
        }

        void Start()
        {
            if (Ui != null) return;
            JobDebugUi dbg = FindFirstObjectByType<JobDebugUi>();
            if (!dbg) dbg = new GameObject("JobDebugUi").AddComponent<JobDebugUi>();
            // The real HUD (NLPrompts/NLToasts) when it is in the scene; else the placeholder for everything.
            Ui = NLJobUiBridge.HudPresent ? new NLJobUiBridge(dbg) : (IJobUi)dbg;
        }

        void OnDestroy() { if (LocalPlayer == this) LocalPlayer = null; }

        void Update()
        {
            if (Ui == null) return;
            JobSlotBook book = JobSlotRegistry.Shared.Book;
            if (book.Held != null || book.ClaimInFlight || Conductor.Busy) { Withdraw(); return; }

            Vector3 p = transform.position;
            SlotInfo current = _offered != null ? book.Get(_offered) : null;
            if (current != null && current.Holder == SlotHolder.Bot && Dist(current, p) <= HideDist) return;   // hysteresis: keep it

            SlotInfo near = book.NearestFree(JobKind.Conductor, p.x, p.z, ShowDist);
            if (near == null) { Withdraw(); return; }
            if (near.Id == _offered) return;
            _offered = near.Id;
            ConductorBay bay = FindBay(near.Id);
            string line = bay && !bay.AtBay ? "Bus never come back. Wait small." : "₦600 and up a trip";
            Ui.Offer(OfferId, PromptKind.Action, "Take over", "Danfo conductor", line, () => Accept(near.Id));
        }

        void Accept(string slotId)
        {
            ConductorBay bay = FindBay(slotId);
            if (!bay) return;
            Withdraw();
            Conductor.Begin(bay);
        }

        void Withdraw()
        {
            if (_offered == null) return;
            Ui.Withdraw(OfferId);
            _offered = null;
        }

        static float Dist(SlotInfo s, Vector3 p) { float dx = s.X - p.x, dz = s.Z - p.z; return Mathf.Sqrt(dx * dx + dz * dz); }

        static ConductorBay FindBay(string slotId)
        {
            foreach (ConductorBay b in FindObjectsByType<ConductorBay>(FindObjectsSortMode.None))
                if (b.Slot && b.Slot.slotId == slotId) return b;
            return null;
        }
    }
}
