// Naija Streets (Unity) — transport-independent halves of the channels.
// Every adapter (Fusion Shared now, Server Mode or our own servers later)
// wires its send/receive into these, so the rules live in one place.
// Spec: docs/specs/netcode-m5.md §3-4. UNVERIFIED (2026-11-05): not compiled yet.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NaijaLife.Network
{
    public delegate bool PoseSender(in NetPose pose, ushort sequence);

    public sealed class ReplicatorCore : IPlayerReplicator
    {
        readonly PoseSender _sendPose;
        readonly Func<PublicState, bool> _sendState;
        readonly SendPolicy _policy = new SendPolicy();
        readonly Dictionary<NetPlayerId, SnapshotBuffer> _remotes = new Dictionary<NetPlayerId, SnapshotBuffer>();
        readonly Dictionary<NetPlayerId, PublicState> _states = new Dictionary<NetPlayerId, PublicState>();
        readonly List<KeyValuePair<NetPlayerId, NetPose>> _latest = new List<KeyValuePair<NetPlayerId, NetPose>>();

        ushort _seq;
        PublicState _pending, _lastSentState;
        bool _hasPending, _stateEverSent;
        double _lastStateSentAt = double.NegativeInfinity;

        public NetPose LastLocalPose { get; private set; }

        public ReplicatorCore(PoseSender sendPose, Func<PublicState, bool> sendState)
        {
            _sendPose = sendPose ?? throw new ArgumentNullException(nameof(sendPose));
            _sendState = sendState ?? throw new ArgumentNullException(nameof(sendState));
        }

        public void SetPaused(bool paused) => _policy.SetPaused(paused);

        public void SubmitLocal(NetPose pose, double now)
        {
            LastLocalPose = pose;
            if (_policy.TryConsume(pose, now))
            {
                unchecked { _seq++; }
                _sendPose(pose, _seq);
            }
            if (_hasPending && now - _lastStateSentAt >= NetLimits.PublicStateMinInterval)
            {
                if (_sendState(_pending))
                {
                    _lastSentState = _pending;
                    _stateEverSent = true;
                    _lastStateSentAt = now;
                    _hasPending = false;
                }
            }
        }

        public void SetLocalPublicState(PublicState state)
        {
            if (_stateEverSent && !_hasPending && state.Equals(_lastSentState)) return;
            _pending = state;
            _hasPending = true;
        }

        public bool TryGetRemotePose(NetPlayerId id, double now, out NetPose pose)
        {
            pose = default;
            return _remotes.TryGetValue(id, out SnapshotBuffer b) && b.TrySample(now, out pose);
        }

        public bool TryGetPublicState(NetPlayerId id, out PublicState state) => _states.TryGetValue(id, out state);

        public void GetNearby(float centerX, float centerZ, List<NetPlayerId> into)
        {
            _latest.Clear();
            foreach (KeyValuePair<NetPlayerId, SnapshotBuffer> kv in _remotes)
                if (!kv.Value.IsHidden && kv.Value.TryGetLatest(out NetPose p))
                    _latest.Add(new KeyValuePair<NetPlayerId, NetPose>(kv.Key, p));
            NearbyPicker.Pick(_latest, centerX, centerZ, NetLimits.MaxNearby, into);
        }

        // ---- called by the adapter ----

        public bool OnRemotePose(NetPlayerId from, in NetPose pose, ushort sequence, double now)
        {
            if (!from.IsValid) return false;
            if (!_remotes.TryGetValue(from, out SnapshotBuffer b)) { b = new SnapshotBuffer(); _remotes[from] = b; }
            return b.Add(pose, sequence, now);
        }

        public bool OnRemotePoseBytes(NetPlayerId from, byte[] bytes, int offset, int length, double now) =>
            MoveCodec.TryDecode(bytes, offset, length, out NetPose p, out ushort seq) && OnRemotePose(from, p, seq, now);

        public void OnRemoteState(NetPlayerId from, PublicState state)
        {
            if (from.IsValid) _states[from] = state;
        }

        public void Remove(NetPlayerId id)
        {
            _remotes.Remove(id);
            _states.Remove(id);
        }

        public void ClearRemotes()
        {
            _remotes.Clear();
            _states.Clear();
        }

        /// <summary>After a reconnect: send pose and public state again at once.</summary>
        public void ResendAll()
        {
            _policy.SetPaused(true);
            _policy.SetPaused(false);
            if (_stateEverSent && !_hasPending) { _pending = _lastSentState; _hasPending = true; }
            _lastStateSentAt = double.NegativeInfinity;
        }
    }

    public sealed class WorldEventsCore : IWorldEvents
    {
        readonly Func<WorldEvent, bool> _send;
        readonly Func<double> _clock;
        readonly RateLimiter _out = new RateLimiter(NetLimits.EventsPerSecond);
        readonly PerSenderLimiter _in = new PerSenderLimiter(NetLimits.EventsPerSecond);

        public event Action<NetPlayerId, WorldEvent> Received;

        public WorldEventsCore(Func<WorldEvent, bool> send, Func<double> clock)
        {
            _send = send ?? throw new ArgumentNullException(nameof(send));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public bool Raise(WorldEventKind kind, ushort arg)
        {
            if (!WorldEvent.IsKnown((byte)kind)) return false;
            if (!_out.TryAcquire(_clock())) return false;
            return _send(new WorldEvent(kind, arg));
        }

        /// <summary>Adapter calls this. <paramref name="from"/> must come from the transport.</summary>
        public void OnRemote(NetPlayerId from, byte kind, ushort arg)
        {
            if (!from.IsValid || !WorldEvent.IsKnown(kind)) return;
            if (!_in.TryAcquire(from, _clock())) return;
            Received?.Invoke(from, new WorldEvent((WorldEventKind)kind, arg));
        }

        public void Forget(NetPlayerId id) => _in.Forget(id);
    }

    public sealed class ChatCore : IChatService
    {
        readonly Func<string, bool> _sendLobby;
        readonly Func<NetPlayerId, string, CancellationToken, Task<bool>> _sendDirect;
        readonly Func<double> _clock;
        readonly ICollection<string> _blocklist;
        readonly RateLimiter _outLobby = new RateLimiter(NetLimits.ChatPerSecond);
        readonly RateLimiter _outDirect = new RateLimiter(NetLimits.ChatPerSecond);
        readonly PerSenderLimiter _inLobby = new PerSenderLimiter(NetLimits.ChatPerSecond);
        readonly PerSenderLimiter _inDirect = new PerSenderLimiter(NetLimits.ChatPerSecond);

        /// <summary>Text is normalized but NOT escaped: the UI must show it with rich text off (or ChatRules.EscapeForRichText).</summary>
        public event Action<NetPlayerId, string> LobbyReceived;
        public event Action<NetPlayerId, string> DirectReceived;

        /// <param name="sendDirect">Null until DMs over Supabase exist (M5.3); DMs then refuse as Offline.</param>
        public ChatCore(Func<string, bool> sendLobby,
                        Func<NetPlayerId, string, CancellationToken, Task<bool>> sendDirect,
                        Func<double> clock,
                        ICollection<string> blocklist)
        {
            _sendLobby = sendLobby ?? throw new ArgumentNullException(nameof(sendLobby));
            _sendDirect = sendDirect;
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _blocklist = blocklist ?? Array.Empty<string>();
        }

        public ChatRefusal SendLobby(string text)
        {
            string line = ChatRules.Normalize(text);
            ChatRefusal r = ChatRules.Check(line, _blocklist);
            if (r != ChatRefusal.None) return r;
            if (!_outLobby.TryAcquire(_clock())) return ChatRefusal.TooFast;
            return _sendLobby(line) ? ChatRefusal.None : ChatRefusal.Offline;
        }

        public async Task<ChatRefusal> SendDirectAsync(NetPlayerId to, string text, CancellationToken cancel)
        {
            if (_sendDirect == null || !to.IsValid) return ChatRefusal.Offline;
            string line = ChatRules.Normalize(text);
            ChatRefusal r = ChatRules.Check(line, _blocklist);
            if (r != ChatRefusal.None) return r;
            if (!_outDirect.TryAcquire(_clock())) return ChatRefusal.TooFast;
            bool ok = await _sendDirect(to, line, cancel);
            return ok ? ChatRefusal.None : ChatRefusal.Offline;
        }

        public void OnLobby(NetPlayerId from, string text) => Deliver(from, text, _inLobby, LobbyReceived);
        public void OnDirect(NetPlayerId from, string text) => Deliver(from, text, _inDirect, DirectReceived);

        void Deliver(NetPlayerId from, string text, PerSenderLimiter limiter, Action<NetPlayerId, string> sink)
        {
            if (!from.IsValid) return;
            string line = ChatRules.Normalize(text);
            if (ChatRules.Check(line, _blocklist) != ChatRefusal.None) return; // also guards modified senders
            if (!limiter.TryAcquire(from, _clock())) return;
            sink?.Invoke(from, line);
        }
    }

    /// <summary>
    /// No network: single-player, no remote players, ever. Used with no connection,
    /// when auth fails, and in offline test scenes. It never invents players.
    /// </summary>
    public sealed class OfflineSession : INetSession
    {
        static readonly NetPlayerId[] None = new NetPlayerId[0];

        readonly ReplicatorCore _players = new ReplicatorCore((in NetPose p, ushort s) => false, s => false);
        readonly WorldEventsCore _events;
        readonly ChatCore _chat;

        public OfflineSession(Func<double> clock)
        {
            _events = new WorldEventsCore(e => false, clock);
            _chat = new ChatCore(t => false, null, clock, null);
        }

        public NetStatus Status => NetStatus.Offline;
        public event Action<NetStatus> StatusChanged { add { } remove { } }
        public NetPlayerId LocalPlayer => default;
        public RoomKey CurrentRoom => default;
        public Task<NetResult> JoinAsync(NetJoinRequest request, CancellationToken cancel) => Task.FromResult(NetResult.NoNetwork);
        public Task LeaveAsync() => Task.CompletedTask;
        public IReadOnlyCollection<NetPlayerId> RemotePlayers => None;
        public event Action<NetPlayerId> PlayerJoined { add { } remove { } }
        public event Action<NetPlayerId> PlayerLeft { add { } remove { } }
        public void SetInterestCenter(float x, float z) { }
        public void SetPaused(bool paused) { }
        public IPlayerReplicator Players => _players;
        public IWorldEvents Events => _events;
        public IChatService Chat => _chat;
        public void Dispose() { }
    }
}
