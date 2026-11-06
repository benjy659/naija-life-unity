// Naija Streets (Unity) — M5.3 part 2: RECEIVE DMs over the Supabase Realtime
// WebSocket (Phoenix protocol, vsn 1.0.0). Pure logic: the socket itself is a
// small IRealtimeSocket in the glue (.jslib in WebGL, ClientWebSocket elsewhere).
//
// Topic rule (live RLS nl_rt_dm_ok): only the two players of d:<lo>:<hi> may
// join it. There is no "all my DMs" channel, so the client joins the topics it
// can expect DMs on: players in the room (PlayerJoined/Left) and anyone it has
// written to or heard from (pinned). At most MaxTopics at once, oldest unpinned
// dropped first. A refused join (guest, block, ban) is not retried for
// RefusedRetrySeconds.
//
// The sender of a DM is the other side of the topic (DmTopic.TryOther). The
// payload's text goes through ChatCore.OnDirect, which normalizes, blocklists
// and rate limits it before the UI sees it.
// Spec: docs/specs/netcode-m5.md §8.1.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace NaijaLife.Network
{
    /// <summary>The glue's WebSocket. Calls come from the main thread; events are raised on it too.</summary>
    public interface IRealtimeSocket
    {
        bool IsOpen { get; }
        /// <summary>Starts connecting. Opened or Closed follows.</summary>
        void Connect();
        void Send(string text);
        void Close();
    }

    /// <summary>Builds and reads Phoenix frames. Topics here are without the "realtime:" prefix.</summary>
    public static class RealtimeProtocol
    {
        public const string Prefix = "realtime:";

        public static string Join(string topic, string accessToken, string reference) =>
            "{\"topic\":" + MiniJson.Quote(Prefix + topic) + ",\"event\":\"phx_join\",\"payload\":{\"config\":" +
            "{\"broadcast\":{\"self\":false,\"ack\":false},\"presence\":{\"key\":\"\"},\"private\":true}," +
            "\"access_token\":" + MiniJson.Quote(accessToken) + "},\"ref\":" + MiniJson.Quote(reference) +
            ",\"join_ref\":" + MiniJson.Quote(reference) + "}";

        public static string Leave(string topic, string reference) =>
            "{\"topic\":" + MiniJson.Quote(Prefix + topic) + ",\"event\":\"phx_leave\",\"payload\":{},\"ref\":" + MiniJson.Quote(reference) + "}";

        public static string Heartbeat(string reference) =>
            "{\"topic\":\"phoenix\",\"event\":\"heartbeat\",\"payload\":{},\"ref\":" + MiniJson.Quote(reference) + "}";

        public static string AccessToken(string topic, string accessToken, string reference) =>
            "{\"topic\":" + MiniJson.Quote(Prefix + topic) + ",\"event\":\"access_token\",\"payload\":{\"access_token\":" +
            MiniJson.Quote(accessToken) + "},\"ref\":" + MiniJson.Quote(reference) + "}";

        public struct Frame
        {
            public string Topic;          // without prefix; "phoenix" for heartbeat replies
            public string Event;          // phx_reply, broadcast, phx_error, phx_close, system, ...
            public string Ref;
            public string Status;         // phx_reply / system: "ok" or "error"
            public string BroadcastEvent; // broadcast: inner event, e.g. "dm"
            public string Text;           // broadcast dm: payload.text
        }

        public static bool TryRead(string json, out Frame f)
        {
            f = default;
            object root = MiniJson.Parse(json);
            if (!(root is Dictionary<string, object>)) return false;
            string topic = MiniJson.Str(root, "topic");
            f.Event = MiniJson.Str(root, "event");
            if (topic == null || f.Event == null) return false;
            f.Topic = topic.StartsWith(Prefix, StringComparison.Ordinal) ? topic.Substring(Prefix.Length) : topic;
            f.Ref = MiniJson.Str(root, "ref");
            Dictionary<string, object> payload = MiniJson.Obj(root, "payload");
            f.Status = MiniJson.Str(payload, "status");
            if (f.Event == "broadcast")
            {
                f.BroadcastEvent = MiniJson.Str(payload, "event");
                f.Text = MiniJson.Str(MiniJson.Obj(payload, "payload"), "text");
            }
            return true;
        }
    }

    public sealed class RealtimeDmClient
    {
        public const int MaxTopics = 60;               // under Realtime's per-connection channel limit
        public const double HeartbeatSeconds = 25;
        public const double HeartbeatTimeoutSeconds = 60;
        public const double JoinTimeoutSeconds = 15;
        public const double RefusedRetrySeconds = 300;

        enum State { Wanted, Joining, Joined, Refused }

        sealed class Topic
        {
            public string Name; public NetPlayerId Other; public State State;
            public string JoinRef; public double Since; public bool Pinned; public double LastUse;
        }

        readonly IRealtimeSocket _socket;
        readonly Func<double> _clock;
        readonly Backoff _backoff;
        readonly Dictionary<string, Topic> _topics = new Dictionary<string, Topic>(StringComparer.Ordinal);

        NetPlayerId _me;
        string _token;
        int _ref;
        bool _connecting;
        double _nextConnectAt, _lastHeartbeatSent, _lastHeartbeatReply, _openedAt;
        string _pendingHeartbeatRef;

        /// <summary>A DM arrived: (sender = other side of the topic, raw text). Feed it to ChatCore.OnDirect.</summary>
        public event Action<NetPlayerId, string> Direct;

        public RealtimeDmClient(IRealtimeSocket socket, Func<double> clock, int seed = 0)
        {
            _socket = socket ?? throw new ArgumentNullException(nameof(socket));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _backoff = new Backoff(seed);
        }

        public int TopicCount => _topics.Count;
        public bool IsJoined(NetPlayerId other) => _me.IsValid && _topics.TryGetValue(DmTopic.For(_me, other) ?? "", out Topic t) && t.State == State.Joined;

        /// <summary>Who I am and my current JWT. A new token is pushed to every joined topic.</summary>
        public void SetIdentity(NetPlayerId me, string accessToken)
        {
            if (!me.Equals(_me)) { _topics.Clear(); if (_socket.IsOpen) _socket.Close(); }
            _me = me;
            bool changed = _token != accessToken;
            _token = accessToken;
            if (changed && _socket.IsOpen && !string.IsNullOrEmpty(accessToken))
                foreach (Topic t in _topics.Values)
                    if (t.State == State.Joined || t.State == State.Joining)
                        _socket.Send(RealtimeProtocol.AccessToken(t.Name, accessToken, NextRef()));
        }

        /// <summary>Listen for DMs from this player. Pinned topics (people I talk to) are dropped last.</summary>
        public void Want(NetPlayerId other, bool pin = false)
        {
            string name = _me.IsValid ? DmTopic.For(_me, other) : null;
            if (name == null) return;
            double now = _clock();
            if (_topics.TryGetValue(name, out Topic t)) { t.Pinned |= pin; t.LastUse = now; return; }
            if (_topics.Count >= MaxTopics && !DropOldest(pin)) return;
            DmTopic.TryOther(name, _me, out NetPlayerId canon); // lowercase form, as the topic has it
            _topics[name] = new Topic { Name = name, Other = canon, State = State.Wanted, Pinned = pin, LastUse = now };
        }

        /// <summary>Stop listening unless pinned (a conversation stays open after the player walks away).</summary>
        public void Unwant(NetPlayerId other)
        {
            string name = _me.IsValid ? DmTopic.For(_me, other) : null;
            if (name == null || !_topics.TryGetValue(name, out Topic t) || t.Pinned) return;
            Remove(t);
        }

        bool DropOldest(bool forPinned)
        {
            Topic oldest = null;
            foreach (Topic t in _topics.Values)
                if ((!t.Pinned || forPinned) && (oldest == null || (oldest.Pinned && !t.Pinned) || (oldest.Pinned == t.Pinned && t.LastUse < oldest.LastUse)))
                    oldest = t;
            if (oldest == null) return false;
            Remove(oldest);
            return true;
        }

        void Remove(Topic t)
        {
            if (_socket.IsOpen && (t.State == State.Joined || t.State == State.Joining))
                _socket.Send(RealtimeProtocol.Leave(t.Name, NextRef()));
            _topics.Remove(t.Name);
        }

        /// <summary>Call every frame (or a few times a second).</summary>
        public void Tick()
        {
            double now = _clock();
            if (!_me.IsValid || string.IsNullOrEmpty(_token)) return;

            if (!_socket.IsOpen)
            {
                if (!_connecting && _topics.Count > 0 && now >= _nextConnectAt)
                {
                    _connecting = true;
                    _socket.Connect();
                }
                return;
            }

            if (now - _lastHeartbeatReply > HeartbeatTimeoutSeconds) { _socket.Close(); return; } // dead link; OnClosed follows
            if (now - _lastHeartbeatSent >= HeartbeatSeconds)
            {
                _pendingHeartbeatRef = NextRef();
                _lastHeartbeatSent = now;
                _socket.Send(RealtimeProtocol.Heartbeat(_pendingHeartbeatRef));
            }

            // Backoff resets only after a stable minute, so a flapping link does not hammer the server.
            if (now - _openedAt >= NetLimits.BackoffResetAfterOnline) _backoff.Reset();

            foreach (Topic t in _topics.Values)
            {
                bool retry = (t.State == State.Wanted) ||
                             (t.State == State.Joining && now - t.Since > JoinTimeoutSeconds) ||
                             (t.State == State.Refused && now - t.Since > RefusedRetrySeconds);
                if (!retry) continue;
                t.JoinRef = NextRef();
                t.State = State.Joining;
                t.Since = now;
                _socket.Send(RealtimeProtocol.Join(t.Name, _token, t.JoinRef));
            }
        }

        public void OnOpened()
        {
            double now = _clock();
            _connecting = false;
            _openedAt = _lastHeartbeatReply = _lastHeartbeatSent = now;
            foreach (Topic t in _topics.Values) t.State = State.Wanted; // rejoin everything
        }

        public void OnClosed()
        {
            _connecting = false;
            foreach (Topic t in _topics.Values) if (t.State != State.Refused) t.State = State.Wanted;
            _nextConnectAt = _clock() + _backoff.Next();
        }

        public void OnMessage(string json)
        {
            if (!RealtimeProtocol.TryRead(json, out RealtimeProtocol.Frame f)) return;
            double now = _clock();

            if (f.Topic == "phoenix")
            {
                if (f.Event == "phx_reply" && f.Ref != null && f.Ref == _pendingHeartbeatRef) _lastHeartbeatReply = now;
                return;
            }
            if (!_topics.TryGetValue(f.Topic, out Topic t)) return; // not a topic I asked for

            switch (f.Event)
            {
                case "phx_reply":
                    if (f.Ref == null || f.Ref != t.JoinRef || t.State != State.Joining) return;
                    t.State = f.Status == "ok" ? State.Joined : State.Refused;
                    t.Since = now;
                    break;
                case "system":
                    if (f.Status == "error") { t.State = State.Refused; t.Since = now; } // e.g. RLS said no
                    break;
                case "phx_error":
                case "phx_close":
                    if (t.State != State.Refused) t.State = State.Wanted;
                    break;
                case "broadcast":
                    if (t.State != State.Joined || f.BroadcastEvent != DmTopic.Event || f.Text == null) return;
                    if (!DmTopic.TryOther(f.Topic, _me, out NetPlayerId from) || !from.Equals(t.Other)) return;
                    t.Pinned = true; t.LastUse = now; // keep listening to people who talk to me
                    Direct?.Invoke(from, f.Text);
                    break;
            }
        }

        string NextRef() => (++_ref).ToString(CultureInfo.InvariantCulture);
    }
}
