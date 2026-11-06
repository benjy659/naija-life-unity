// Naija Streets (Unity) - one danfo bay: the bus, its route, its queue and
// its bot conductor (docs/specs/jobs-m4.md sections 6.4 and 10). While a bot
// holds the slot the bay runs bot trips on a timer (no money, no RPC), so
// the park looks alive with no player. While a player holds it the bus waits
// for ConductorShift to drive the trips.

using System.Collections;
using NaijaLife.Jobs;
using UnityEngine;

namespace NaijaLife.JobsGlue
{
    [RequireComponent(typeof(JobSlot))]
    public sealed class ConductorBay : MonoBehaviour
    {
        public Transform bus;
        [Tooltip("Where the conductor hangs at the sliding door (child of the bus).")]
        public Transform doorSeat;
        [Tooltip("Ground point beside the door, on the island.")]
        public Transform doorStep;
        public DanfoRoute route;
        public PassengerQueue queue;
        [Tooltip("Route name painted on the bus, e.g. UNILAG GATE.")]
        public string routeName = "UNILAG GATE";
        [Tooltip("Stagger bot departures between bays.")]
        public float firstBotTripDelay = 5f;

        public JobSlot Slot { get; private set; }
        public bool AtBay => !route || !route.Running;
        public bool BotTripRunning { get; private set; }

        Coroutine _botLoop;

        void Awake() { Slot = GetComponent<JobSlot>(); }

        void OnEnable() { _botLoop = StartCoroutine(BotLoop()); }
        void OnDisable() { if (_botLoop != null) StopCoroutine(_botLoop); }

        IEnumerator BotLoop()
        {
            yield return new WaitForSeconds(firstBotTripDelay);
            while (true)
            {
                if (Slot.Holder != SlotHolder.Bot || !Slot.bot) { yield return null; continue; }
                BotTripRunning = true;
                // Load for 30 to 45 s; a passenger answers the bot's shout every 2.5 to 4 s.
                float loadUntil = Time.time + Random.Range(30f, 45f);
                while (Time.time < loadUntil && queue && queue.Boarded < ShiftRules.Seats)
                {
                    if (Slot.Holder != SlotHolder.Bot) break;
                    queue.CallOne();
                    yield return new WaitForSeconds(Random.Range(2.5f, 4f));
                }
                if (Slot.Holder != SlotHolder.Bot) { BotTripRunning = false; continue; }
                // Bot boards at the door and the bus does the route.
                bool atDoor = false;
                Slot.bot.WalkToDoor(doorStep ? doorStep : Slot.workPoint, () => atDoor = true);
                while (!atDoor && Slot.Holder == SlotHolder.Bot) yield return null;
                if (Slot.Holder != SlotHolder.Bot) { BotTripRunning = false; continue; }
                Slot.bot.Ride(doorSeat ? doorSeat : bus);
                if (route) yield return route.Drive(bus);   // passengers get down at the stop (DropPassengers)
                if (queue) queue.ResetBoarded();
                Slot.bot.Alight(doorStep ? doorStep.position : bus.position);
                Slot.bot.WalkBack(Slot.workPoint);
                BotTripRunning = false;
                yield return new WaitForSeconds(Random.Range(8f, 15f));
            }
        }

        /// <summary>Called by DanfoRoute stop handlers: passengers walk off at the lay-by.</summary>
        public void DropPassengers(int count)
        {
            if (!queue || !bus) return;
            Vector3 door = doorSeat ? doorSeat.position : bus.position;
            queue.Alight(new Vector3(door.x, 0f, door.z), bus.right, count);
        }

        void Start()
        {
            if (route) route.ReachedStop += () => DropPassengers(queue ? queue.Boarded : 0);
        }
    }
}
