// Naija Streets (Unity) — INetSession over Photon Fusion 2, Shared Mode.
// Spec: docs/specs/netcode-m5.md. The ONLY folder allowed to say "using Fusion".
//
// SKELETON, UNVERIFIED (2026-11-05): written from the Fusion 2 docs without the
// SDK installed. This assembly only compiles when the scripting define NL_FUSION
// is set (asmdef defineConstraints). Ted's team: task M5.1c = fix calls against
// the installed SDK, keep the shape (what goes over the wire, who stamps ids).
//
// Wire contents (all of it): packed pose + sequence, PublicState (3 ushorts),
// world events (kind byte + ushort), lobby chat text. Never NIN, email, date of
// birth, money, home location or device id.

#if NL_FUSION
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Fusion;
using Fusion.Photon.Realtime;
using UnityEngine;

namespace NaijaLife.Network.Fusion
{
    public sealed class FusionNetSession : INetSession, INetworkRunnerCallbacks
    {
        public const string FixedRegion = "eu"; // one region for all of Nigeria (M5.2 verdict)

        readonly GameObject _host;
        readonly NetworkPrefabRef _avatarPrefab;
        readonly Func<double> _clock;
        readonly ReplicatorCore _players;
        readonly WorldEventsCore _events;
        readonly ChatCore _chat;
        readonly Dictionary<PlayerRef, NetPlayerId> _ids = new Dictionary<PlayerRef, NetPlayerId>();
        readonly HashSet<NetPlayerId> _remote = new HashSet<NetPlayerId>();
        readonly Backoff _backoff = new Backoff();

        NetworkRunner _runner;
        NetAvatar _localAvatar;
        NetJoinRequest _lastRequest;
        NetStatus _status = NetStatus.Offline;
        bool _leaving;
        double _onlineSince;

        public float InterestX { get; private set; }
        public float InterestZ { get; private set; }

        /// <param name="sendDirect">DM sender over Supabase (SupabaseDirectMessages.SendAsync). Null: DMs refuse as Offline.</param>
        public FusionNetSession(GameObject host, NetworkPrefabRef avatarPrefab, Func<double> clock,
                                ICollection<string> chatBlocklist,
                                Func<NetPlayerId, string, CancellationToken, Task<bool>> sendDirect = null)
        {
            _host = host;
            _avatarPrefab = avatarPrefab;
            _clock = clock;
            _players = new ReplicatorCore(SendPose, SendState);
            _events = new WorldEventsCore(SendEvent, clock);
            _chat = new ChatCore(SendLobby, sendDirect, clock, chatBlocklist); // DMs over Supabase, never Fusion (M5.3)
        }

        // ---------------- INetSession ----------------

        public NetStatus Status => _status;
        public event Action<NetStatus> StatusChanged;
        public NetPlayerId LocalPlayer { get; private set; }
        public RoomKey CurrentRoom { get; private set; }
        public IReadOnlyCollection<NetPlayerId> RemotePlayers => _remote;
        public event Action<NetPlayerId> PlayerJoined;
        public event Action<NetPlayerId> PlayerLeft;
        public IPlayerReplicator Players => _players;
        public IWorldEvents Events => _events;
        public IChatService Chat => _chat;

        internal ReplicatorCore PlayersCore => _players;
        internal WorldEventsCore EventsCore => _events;
        internal ChatCore ChatCore => _chat;
        internal double Now => _clock();

        public void SetInterestCenter(float x, float z) { InterestX = x; InterestZ = z; }
        public void SetPaused(bool paused) => _players.SetPaused(paused);

        public async Task<NetResult> JoinAsync(NetJoinRequest request, CancellationToken cancel)
        {
            if (request == null || string.IsNullOrEmpty(request.AccessToken)) return NetResult.AuthFailed;
            _lastRequest = request;
            _leaving = false;
            SetStatus(NetStatus.Connecting);

            RoomKey room = request.Room;
            for (int tries = 0; tries < 5; tries++) // room full -> next instance
            {
                NetResult r = await StartRunner(room, request, cancel);
                if (r == NetResult.Ok) { CurrentRoom = room; return r; }
                if (r != NetResult.RoomFull) { SetStatus(NetStatus.Failed); return r; }
                room = room.Next();
            }
            SetStatus(NetStatus.Failed);
            return NetResult.RoomFull;
        }

        public async Task LeaveAsync()
        {
            _leaving = true;
            if (_runner != null) await _runner.Shutdown();
            ResetRoomState();
            SetStatus(NetStatus.Offline);
        }

        public void Dispose()
        {
            _leaving = true;
            if (_runner != null) _runner.Shutdown();
        }

        // ---------------- start / reconnect ----------------

        async Task<NetResult> StartRunner(RoomKey room, NetJoinRequest req, CancellationToken cancel)
        {
            if (_runner != null) { await _runner.Shutdown(); }
            var go = new GameObject("NetRunner " + room);
            go.transform.SetParent(_host.transform, false);
            _runner = go.AddComponent<NetworkRunner>();
            _runner.ProvideInput = false;
            _runner.AddCallbacks(this);

            // Server-side identity: the Supabase JWT goes to Photon custom auth
            // (Edge Function photon-auth), which returns UserId = auth.uid().
            var auth = new AuthenticationValues { AuthType = CustomAuthenticationType.Custom };
            auth.SetAuthPostData(new Dictionary<string, object> { { "token", req.AccessToken } });

            FusionAppSettings app = PhotonAppSettings.Global.AppSettings.GetCopy();
            app.FixedRegion = FixedRegion;

            var args = new StartGameArgs
            {
                GameMode = GameMode.Shared,
                SessionName = room.ToString(),
                PlayerCount = req.MaxPlayers,
                AuthValues = auth,
                CustomPhotonAppSettings = app,
                SceneManager = go.AddComponent<NetworkSceneManagerDefault>(),
                StartGameCancellationToken = cancel,
            };

            StartGameResult res;
            try { res = await _runner.StartGame(args); }
            catch (OperationCanceledException) { return NetResult.Cancelled; }
            catch (Exception e) { Debug.LogWarning("[net] start failed: " + e.Message); return NetResult.Error; }

            if (!res.Ok) return MapShutdown(res.ShutdownReason);

            LocalPlayer = new NetPlayerId(_runner.GetPlayerUserId(_runner.LocalPlayer));
            if (!LocalPlayer.IsValid) { await _runner.Shutdown(); return NetResult.AuthFailed; }

            NetworkObject obj = _runner.Spawn(_avatarPrefab, Vector3.zero, Quaternion.identity, _runner.LocalPlayer);
            _localAvatar = obj.GetComponent<NetAvatar>();
            _players.ResendAll();
            _onlineSince = _clock();
            SetStatus(NetStatus.Online);
            return NetResult.Ok;
        }

        static NetResult MapShutdown(ShutdownReason r)
        {
            switch (r)
            {
                case ShutdownReason.GameIsFull: return NetResult.RoomFull;
                case ShutdownReason.CustomAuthenticationFailed: return NetResult.AuthFailed;
                case ShutdownReason.ConnectionTimeout: return NetResult.Timeout;
                case ShutdownReason.PhotonCloudTimeout: return NetResult.NoNetwork;
                default: return NetResult.Error;
            }
        }

        async void ReconnectLoop()
        {
            SetStatus(NetStatus.Reconnecting);
            // Backoff only resets after 60 s online, so a flapping link does not hammer Photon.
            if (_clock() - _onlineSince >= NetLimits.BackoffResetAfterOnline) _backoff.Reset();
            while (!_leaving && _lastRequest != null)
            {
                double wait = _backoff.Next();
                await Awaitable.WaitForSecondsAsync((float)wait); // works on single-threaded WebGL
                if (_leaving) return;
                NetResult r = await StartRunner(CurrentRoom, _lastRequest, CancellationToken.None);
                if (r == NetResult.Ok) return;
                if (r == NetResult.AuthFailed) { SetStatus(NetStatus.Failed); return; } // token expired: bootstrap signs in again
            }
        }

        void ResetRoomState()
        {
            foreach (NetPlayerId id in _remote) PlayerLeft?.Invoke(id);
            _remote.Clear();
            _ids.Clear();
            _players.ClearRemotes();
            _localAvatar = null;
            _runner = null;
        }

        void SetStatus(NetStatus s)
        {
            if (_status == s) return;
            _status = s;
            StatusChanged?.Invoke(s);
        }

        // ---------------- outgoing ----------------

        bool SendPose(in NetPose pose, ushort seq)
        {
            if (_localAvatar == null || _status != NetStatus.Online) return false;
            _localAvatar.WritePose(pose, seq);
            return true;
        }

        bool SendState(PublicState s)
        {
            if (_localAvatar == null || _status != NetStatus.Online) return false;
            _localAvatar.WriteState(s);
            return true;
        }

        bool SendEvent(WorldEvent e)
        {
            if (_localAvatar == null || _status != NetStatus.Online) return false;
            _localAvatar.RpcEvent((byte)e.Kind, e.Arg);
            return true;
        }

        bool SendLobby(string text)
        {
            if (_localAvatar == null || _status != NetStatus.Online) return false;
            _localAvatar.RpcLobby(text);
            return true;
        }

        // ---------------- incoming (from NetAvatar) ----------------

        /// <summary>The sender id is looked up from the transport's PlayerRef, never from data.</summary>
        internal bool TryGetId(PlayerRef p, out NetPlayerId id)
        {
            if (_ids.TryGetValue(p, out id)) return true;
            if (_runner == null) return false;
            string uid = _runner.GetPlayerUserId(p);
            if (string.IsNullOrEmpty(uid)) return false;
            id = new NetPlayerId(uid);
            _ids[p] = id;
            return true;
        }

        // ---------------- INetworkRunnerCallbacks ----------------

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            if (player == runner.LocalPlayer) return;
            if (TryGetId(player, out NetPlayerId id) && _remote.Add(id)) PlayerJoined?.Invoke(id);
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            if (!_ids.TryGetValue(player, out NetPlayerId id)) return;
            _ids.Remove(player);
            _players.Remove(id);
            _events.Forget(id);
            if (_remote.Remove(id)) PlayerLeft?.Invoke(id);
        }

        public void OnShutdown(NetworkRunner runner, ShutdownReason reason)
        {
            if (runner != _runner) return;
            ResetRoomState();
            if (_leaving) { SetStatus(NetStatus.Offline); return; }
            if (_status == NetStatus.Online) ReconnectLoop();
        }

        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
            // OnShutdown follows and drives the reconnect.
        }

        public void OnConnectedToServer(NetworkRunner runner) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnInput(NetworkRunner runner, NetworkInput input) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }

        internal static FusionNetSession Of(NetworkRunner runner)
        {
            var host = runner != null ? runner.GetComponentInParent<FusionSessionHost>() : null;
            return host != null ? host.Session : null;
        }
    }
}
#endif
