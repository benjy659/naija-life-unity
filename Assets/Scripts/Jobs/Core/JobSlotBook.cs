// Naija Streets (Unity) - the job-slot book: who holds which job
// (docs/specs/jobs-m4.md section 4). Every job in the game goes through this
// one class (rule JS-7). Pure C#; JobSlotRegistry (Unity) wraps it.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NaijaLife.Jobs
{
    public enum SlotHolder { Bot = 0, Local = 1, Remote = 2 }

    public enum ClaimResult { Ok, Busy, Taken, TooFar, NoPawa, ShiftCap, Offline, Refused, Unknown }

    public enum ReleaseReason { WalkedAway, Idle, ShiftEnded, PawaEmpty, OtherAction, Disconnected }

    public sealed class SlotInfo
    {
        public string Id;
        public JobKind Kind;
        public float X, Z;            // work point, world metres
        public float TakeRadius = 2.6f;
        public SlotHolder Holder = SlotHolder.Bot;
    }

    /// <summary>Network seam for other players' claims (rule JS-6). M5 adds the Fusion one.</summary>
    public interface IJobSlotSync
    {
        /// <summary>Ask the room for the slot. True when this player got it.</summary>
        Task<bool> RequestClaimAsync(string slotId, CancellationToken cancel);
        void Release(string slotId);
        /// <summary>Another player took (true) or let go of (false) a slot.</summary>
        event Action<string, bool> RemoteChanged;
    }

    /// <summary>Single player: every claim is confirmed at once.</summary>
    public sealed class LocalJobSlotSync : IJobSlotSync
    {
        public Task<bool> RequestClaimAsync(string slotId, CancellationToken cancel) => Task.FromResult(true);
        public void Release(string slotId) { }
#pragma warning disable 67
        public event Action<string, bool> RemoteChanged;
#pragma warning restore 67
    }

    public sealed class JobSlotBook
    {
        readonly Dictionary<string, SlotInfo> _slots = new Dictionary<string, SlotInfo>();
        readonly IJobSlotSync _sync;
        string _held;        // local player's slot id
        string _claiming;    // claim in flight

        /// <summary>(slot, new holder).</summary>
        public event Action<SlotInfo, SlotHolder> SlotChanged;
        /// <summary>(slot, why) after the local player lets go.</summary>
        public event Action<SlotInfo, ReleaseReason> Released;

        public JobSlotBook(IJobSlotSync sync = null)
        {
            _sync = sync ?? new LocalJobSlotSync();
            _sync.RemoteChanged += OnRemote;
        }

        public int Count => _slots.Count;
        public SlotInfo Held => _held != null && _slots.TryGetValue(_held, out SlotInfo s) ? s : null;
        public bool ClaimInFlight => _claiming != null;

        public void Register(SlotInfo slot)
        {
            if (slot == null || string.IsNullOrEmpty(slot.Id)) throw new ArgumentException("slot needs an id");
            if (_slots.ContainsKey(slot.Id)) throw new ArgumentException("duplicate slot id " + slot.Id);
            _slots[slot.Id] = slot;
        }

        public void Unregister(string id)
        {
            if (id == null) return;
            if (_held == id) Release(id, ReleaseReason.Disconnected);
            _slots.Remove(id);
        }

        public SlotInfo Get(string id) => id != null && _slots.TryGetValue(id, out SlotInfo s) ? s : null;

        public IEnumerable<SlotInfo> All => _slots.Values;

        /// <summary>A bot holds it and the local player holds nothing.</summary>
        public bool Available(string id)
        {
            SlotInfo s = Get(id);
            return s != null && s.Holder == SlotHolder.Bot && _held == null && _claiming == null;
        }

        /// <summary>Nearest slot of this kind a bot holds, within maxDist. Null if none or if the player already holds one.</summary>
        public SlotInfo NearestFree(JobKind kind, float x, float z, float maxDist)
        {
            if (_held != null || _claiming != null) return null;
            SlotInfo best = null;
            float bestD2 = maxDist * maxDist;
            foreach (SlotInfo s in _slots.Values)
            {
                if (s.Kind != kind || s.Holder != SlotHolder.Bot) continue;
                float dx = s.X - x, dz = s.Z - z, d2 = dx * dx + dz * dz;
                if (d2 <= bestD2) { bestD2 = d2; best = s; }
            }
            return best;
        }

        /// <summary>
        /// Claim in the spec's order: Busy, Taken, TooFar, then the job's own gate
        /// (Pawa, shift start), then the room. Nothing changes unless all pass.
        /// </summary>
        public async Task<ClaimResult> TryClaimAsync(string id, float playerX, float playerZ,
            Func<CancellationToken, Task<ClaimResult>> gate, CancellationToken cancel)
        {
            SlotInfo s = Get(id);
            if (s == null) return ClaimResult.Unknown;
            if (_held != null || _claiming != null) return ClaimResult.Busy;
            if (s.Holder != SlotHolder.Bot) return ClaimResult.Taken;
            float dx = s.X - playerX, dz = s.Z - playerZ;
            if (dx * dx + dz * dz > s.TakeRadius * s.TakeRadius) return ClaimResult.TooFar;

            _claiming = id;
            try
            {
                if (gate != null)
                {
                    ClaimResult g = await gate(cancel);
                    if (g != ClaimResult.Ok) return g;
                }
                bool got;
                try { got = await _sync.RequestClaimAsync(id, cancel); }
                catch (OperationCanceledException) { throw; }
                catch (Exception) { return ClaimResult.Offline; }
                if (!got) return ClaimResult.Taken;
                if (cancel.IsCancellationRequested || s.Holder != SlotHolder.Bot || !_slots.ContainsKey(id))
                {
                    _sync.Release(id);
                    return ClaimResult.Taken;
                }
                _held = id;
                SetHolder(s, SlotHolder.Local);
                return ClaimResult.Ok;
            }
            finally { _claiming = null; }
        }

        public void Release(string id, ReleaseReason why)
        {
            if (id == null || _held != id) return;
            SlotInfo s = Get(id);
            _held = null;
            _sync.Release(id);
            if (s != null)
            {
                SetHolder(s, SlotHolder.Bot);
                Released?.Invoke(s, why);
            }
        }

        void OnRemote(string id, bool held)
        {
            SlotInfo s = Get(id);
            if (s == null || id == _held) return;
            SetHolder(s, held ? SlotHolder.Remote : SlotHolder.Bot);
        }

        void SetHolder(SlotInfo s, SlotHolder h)
        {
            if (s.Holder == h) return;
            s.Holder = h;
            SlotChanged?.Invoke(s, h);
        }
    }
}
