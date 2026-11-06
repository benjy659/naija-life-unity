// Naija Streets (Unity) - one bot worker's job (docs/specs/jobs-m4.md section 4).
// Put on the bay object. WorkPoint = where the bot works (at the danfo door);
// two aside points along the island (Oshodi layout: aside-west, aside-east).

using NaijaLife.Jobs;
using UnityEngine;

namespace NaijaLife.JobsGlue
{
    public sealed class JobSlot : MonoBehaviour
    {
        [Tooltip("Stable id, e.g. oshodi-bay-1-conductor. Never rename once live.")]
        public string slotId = "oshodi-bay-1-conductor";
        public JobDefinition definition;
        public Transform workPoint;
        public Transform asideWest;
        public Transform asideEast;
        public BotWorker bot;

        public SlotInfo Info { get; private set; }
        public SlotHolder Holder => Info != null ? Info.Holder : SlotHolder.Bot;

        void OnEnable()
        {
            Vector3 p = workPoint ? workPoint.position : transform.position;
            Info = new SlotInfo
            {
                Id = slotId,
                Kind = definition ? definition.kind : JobKind.Conductor,
                X = p.x, Z = p.z,
                TakeRadius = definition ? definition.takeRadius : 2.6f,
            };
            JobSlotRegistry.Shared.Book.Register(Info);
            JobSlotRegistry.Shared.Book.SlotChanged += OnChanged;
        }

        void OnDisable()
        {
            if (Info == null) return;
            JobSlotRegistry.Shared.Book.SlotChanged -= OnChanged;
            JobSlotRegistry.Shared.Book.Unregister(Info.Id);
            Info = null;
        }

        /// <summary>The aside point farther from where the player is, so the bot never stands between the player and the door.</summary>
        public Transform AsideFor(Vector3 playerPos)
        {
            if (!asideWest) return asideEast;
            if (!asideEast) return asideWest;
            float w = (asideWest.position - playerPos).sqrMagnitude;
            float e = (asideEast.position - playerPos).sqrMagnitude;
            return w >= e ? asideWest : asideEast;
        }

        void OnChanged(SlotInfo s, SlotHolder h)
        {
            if (s != Info || !bot) return;
            if (h == SlotHolder.Bot) bot.WalkBack(workPoint);
            else
            {
                Transform player = PlayerJobs.LocalPlayer ? PlayerJobs.LocalPlayer.transform : null;
                bot.StepAside(AsideFor(player ? player.position : transform.position), h == SlotHolder.Local);
            }
        }

        void OnDrawGizmos()
        {
            if (workPoint) { Gizmos.color = Color.green; Gizmos.DrawWireSphere(workPoint.position, 0.3f); }
            Gizmos.color = Color.yellow;
            if (asideWest) Gizmos.DrawWireSphere(asideWest.position, 0.25f);
            if (asideEast) Gizmos.DrawWireSphere(asideEast.position, 0.25f);
        }
    }
}
