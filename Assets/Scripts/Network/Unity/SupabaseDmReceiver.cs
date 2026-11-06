// Naija Streets (Unity) — M5.3 part 2: receives DMs over the Supabase Realtime
// WebSocket and hands them to the session's chat (ChatCore.OnDirect), which
// normalizes, blocklists and rate limits them before DirectReceived fires.
// Listens to: every player in the room (joined/left) and anyone I wrote to or
// heard from (kept open). Guests (anonymous sign-in) never connect: the live
// RLS refuses them anyway.
// NetBootstrap adds this after sign-in. Spec: docs/specs/netcode-m5.md §8.1.
// UNVERIFIED (2026-11-06): not compiled in Unity or run against the server yet.

using NaijaLife.Network;
using UnityEngine;

namespace NaijaLife.NetworkGlue
{
    public sealed class SupabaseDmReceiver : MonoBehaviour
    {
        const float TokenCheckSeconds = 30f; // GetAccessTokenAsync refreshes 60 s before expiry

        SupabaseSession _sb;
        INetSession _session;
        SupabaseDirectMessages _dms;
        RealtimeSocket _socket;
        RealtimeDmClient _client;
        float _nextTokenCheck;
        bool _checking, _listening;

        public RealtimeDmClient Client => _client;

        public void Init(SupabaseSession sb, INetSession session, SupabaseDirectMessages dms)
        {
            Shutdown();
            _sb = sb; _session = session; _dms = dms;
            _socket = new RealtimeSocket(SocketUrl);
#if UNITY_WEBGL && !UNITY_EDITOR
            _socket.ReceiverObject = gameObject.name;
#endif
            _client = new RealtimeDmClient(_socket, () => Time.realtimeSinceStartupAsDouble);
            _socket.Opened += _client.OnOpened;
            _socket.Message += _client.OnMessage;
            _socket.Closed += _client.OnClosed;
            _client.Direct += OnDirect;

            if (_session != null)
            {
                _session.PlayerJoined += OnPlayerJoined;
                _session.PlayerLeft += OnPlayerLeft;
            }
            if (_dms != null) _dms.Sent += OnSent;
            _nextTokenCheck = 0f; _listening = false; // identity first, then the room's players
        }

        string SocketUrl()
        {
            string http = _sb.Config.Url.TrimEnd('/');
            string ws = http.StartsWith("https://") ? "wss://" + http.Substring(8) : "ws://" + http.Substring(http.IndexOf("://") + 3);
            // The anon key is public by design; the player's JWT goes in each join, not the URL.
            return ws + "/realtime/v1/websocket?apikey=" + System.Uri.EscapeDataString(_sb.Config.AnonKey) + "&vsn=1.0.0";
        }

        async void CheckToken()
        {
            _checking = true;
            string token = await _sb.GetAccessTokenAsync();
            _checking = false;
            if (_client == null) return;
            bool canDm = token != null && !_sb.IsAnonymous && DmTopic.IsUuid(_sb.UserId);
            _client.SetIdentity(canDm ? new NetPlayerId(_sb.UserId) : default, canDm ? token : null);
            if (canDm && !_listening && _session != null)
                foreach (NetPlayerId p in _session.RemotePlayers) _client.Want(p); // players already in the room
            _listening = canDm;
        }

        void Update()
        {
            if (_client == null) return;
            if (!_checking && Time.realtimeSinceStartup >= _nextTokenCheck)
            {
                _nextTokenCheck = Time.realtimeSinceStartup + TokenCheckSeconds;
                CheckToken();
            }
            _client.Tick();
        }

        void OnPlayerJoined(NetPlayerId p) => _client?.Want(p);
        void OnPlayerLeft(NetPlayerId p) => _client?.Unwant(p);
        void OnSent(NetPlayerId p) => _client?.Want(p, pin: true);

        void OnDirect(NetPlayerId from, string text)
        {
            if (_session != null && _session.Chat is ChatCore chat) chat.OnDirect(from, text);
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        // From NLRealtime.jslib via SendMessage.
        void OnSocketOpen(string _) => _socket?.RaiseOpened();
        void OnSocketMessage(string text) => _socket?.RaiseMessage(text);
        void OnSocketClose(string _) => _socket?.RaiseClosed();
#endif

        void Shutdown()
        {
            if (_session != null) { _session.PlayerJoined -= OnPlayerJoined; _session.PlayerLeft -= OnPlayerLeft; }
            if (_dms != null) _dms.Sent -= OnSent;
            if (_client != null) _client.Direct -= OnDirect;
            RealtimeSocket s = _socket;
            _socket = null; _client = null;
            s?.Close();
        }

        void OnDestroy() => Shutdown();
    }
}
