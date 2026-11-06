// Edit Mode tests for M5.3 part 2: MiniJson, Phoenix frames and the DM receive
// state machine, with a fake socket (no network).

using System.Collections.Generic;
using NUnit.Framework;

namespace NaijaLife.Network.Tests
{
    public class RealtimeDmTests
    {
        const string Me = "11111111-2222-3333-4444-555555555555";
        const string You = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";
        const string Them = "bbbbbbbb-bbbb-cccc-dddd-eeeeeeeeeeee";
        static readonly string Topic = "d:" + Me + ":" + You;

        sealed class FakeSocket : IRealtimeSocket
        {
            public bool Open; public int Connects; public readonly List<string> Sent = new List<string>();
            public System.Action OnClose;
            public bool IsOpen => Open;
            public void Connect() => Connects++;
            public void Send(string text) => Sent.Add(text);
            public void Close() { Open = false; OnClose?.Invoke(); }
        }

        static string RefOf(string frame) => MiniJson.Str(MiniJson.Parse(frame), "ref");

        static string Reply(string topic, string reference, string status) =>
            "{\"topic\":\"realtime:" + topic + "\",\"event\":\"phx_reply\",\"payload\":{\"status\":\"" + status + "\",\"response\":{}},\"ref\":\"" + reference + "\"}";

        static string Dm(string topic, string text) =>
            "{\"topic\":\"realtime:" + topic + "\",\"event\":\"broadcast\",\"payload\":{\"type\":\"broadcast\",\"event\":\"dm\",\"payload\":{\"text\":" + MiniJson.Quote(text) + ",\"ts\":1}},\"ref\":null}";

        sealed class Rig { public RealtimeDmClient C; public FakeSocket S; public double[] Now; }

        static Rig Make()
        {
            var r = new Rig { Now = new double[] { 100 }, S = new FakeSocket() };
            double[] now = r.Now;
            r.C = new RealtimeDmClient(r.S, () => now[0], seed: 7);
            r.S.OnClose = r.C.OnClosed;
            r.C.SetIdentity(new NetPlayerId(Me), "jwt-1");
            return r;
        }

        [Test]
        public void MiniJson_ReadsNestedAndEscapes()
        {
            object o = MiniJson.Parse("{\"a\":{\"b\":\"x\\\"y\\u00e9\"},\"n\":[1,2.5,true,null]}");
            Assert.AreEqual("x\"y\u00e9", MiniJson.Str(MiniJson.Obj(o, "a"), "b"));
            Assert.IsNull(MiniJson.Parse("{\"a\":"));
            Assert.IsNull(MiniJson.Parse("{} extra"));
            string q = MiniJson.Quote("line\n\"quote\"\\");
            Assert.AreEqual("line\n\"quote\"\\", MiniJson.Parse(q));
        }

        [Test]
        public void Join_IsPrivate_AndCarriesToken()
        {
            object o = MiniJson.Parse(RealtimeProtocol.Join(Topic, "tok\"en", "5"));
            Assert.AreEqual("realtime:" + Topic, MiniJson.Str(o, "topic"));
            Assert.AreEqual("phx_join", MiniJson.Str(o, "event"));
            var payload = MiniJson.Obj(o, "payload");
            Assert.AreEqual("tok\"en", MiniJson.Str(payload, "access_token"));
            Assert.AreEqual(true, MiniJson.Obj(payload, "config")["private"]);
        }

        [Test]
        public void Client_ConnectsOnlyWhenItHasTopics_ThenJoins()
        {
            var r = Make(); var c = r.C; var s = r.S;
            c.Tick();
            Assert.AreEqual(0, s.Connects, "nothing to listen to yet");
            c.Want(new NetPlayerId(You));
            c.Tick();
            Assert.AreEqual(1, s.Connects);
            s.Open = true; c.OnOpened(); c.Tick();
            Assert.IsTrue(s.Sent.Exists(f => f.Contains("phx_join") && f.Contains(Topic)));
        }

        [Test]
        public void Client_DeliversDm_OnlyAfterJoin_FromTheOtherSide()
        {
            var r = Make(); var c = r.C; var s = r.S;
            NetPlayerId got = default; string text = null;
            c.Direct += (from, t) => { got = from; text = t; };
            c.Want(new NetPlayerId(You));
            c.Tick(); s.Open = true; c.OnOpened(); c.Tick();
            string join = s.Sent.Find(f => f.Contains("phx_join"));

            c.OnMessage(Dm(Topic, "early"));
            Assert.IsNull(text, "not joined yet");

            c.OnMessage(Reply(Topic, RefOf(join), "ok"));
            Assert.IsTrue(c.IsJoined(new NetPlayerId(You)));
            c.OnMessage(Dm(Topic, "how far"));
            Assert.AreEqual(new NetPlayerId(You), got);
            Assert.AreEqual("how far", text);

            text = null;
            c.OnMessage(Dm("d:" + Me + ":" + Them, "not joined topic"));
            Assert.IsNull(text);
        }

        [Test]
        public void Client_RefusedJoin_WaitsBeforeRetry()
        {
            var r = Make(); var c = r.C; var s = r.S; var now = r.Now;
            c.Want(new NetPlayerId(You));
            c.Tick(); s.Open = true; c.OnOpened(); c.Tick();
            string join = s.Sent.Find(f => f.Contains("phx_join"));
            c.OnMessage(Reply(Topic, RefOf(join), "error"));
            s.Sent.Clear();
            now[0] += 20; c.Tick();
            Assert.IsFalse(s.Sent.Exists(f => f.Contains("phx_join")), "no hammering after a refusal");
            // Keep the link alive (answer heartbeats) until the refusal wait is over.
            for (double t = 0; t <= RealtimeDmClient.RefusedRetrySeconds; t += 20)
            {
                now[0] += 20; c.Tick();
                string hb = s.Sent.FindLast(f => f.Contains("heartbeat"));
                if (hb != null) c.OnMessage("{\"topic\":\"phoenix\",\"event\":\"phx_reply\",\"payload\":{\"status\":\"ok\"},\"ref\":\"" + RefOf(hb) + "\"}");
            }
            Assert.IsTrue(s.IsOpen, "heartbeats answered, link stays up");
            Assert.IsTrue(s.Sent.Exists(f => f.Contains("phx_join")));
        }

        [Test]
        public void Client_HeartbeatTimeout_ClosesAndBacksOff()
        {
            var r = Make(); var c = r.C; var s = r.S; var now = r.Now;
            c.Want(new NetPlayerId(You));
            c.Tick(); s.Open = true; c.OnOpened(); c.Tick();
            now[0] += RealtimeDmClient.HeartbeatSeconds; c.Tick();
            Assert.IsTrue(s.Sent.Exists(f => f.Contains("heartbeat")));
            now[0] += RealtimeDmClient.HeartbeatTimeoutSeconds; c.Tick();
            Assert.IsFalse(s.IsOpen, "dead link closed");
            int connects = s.Connects;
            c.Tick();
            Assert.AreEqual(connects, s.Connects, "waits for backoff");
            now[0] += 120; c.Tick();
            Assert.AreEqual(connects + 1, s.Connects);
        }

        [Test]
        public void Client_NewToken_IsPushedToJoinedTopics()
        {
            var r = Make(); var c = r.C; var s = r.S;
            c.Want(new NetPlayerId(You));
            c.Tick(); s.Open = true; c.OnOpened(); c.Tick();
            c.OnMessage(Reply(Topic, RefOf(s.Sent.Find(f => f.Contains("phx_join"))), "ok"));
            c.SetIdentity(new NetPlayerId(Me), "jwt-2");
            Assert.IsTrue(s.Sent.Exists(f => f.Contains("access_token") && f.Contains("jwt-2") && f.Contains("\"event\":\"access_token\"")));
        }

        [Test]
        public void Client_UnwantKeepsPinnedConversations_AndCapsTopics()
        {
            var r = Make(); var c = r.C; var now = r.Now;
            c.Want(new NetPlayerId(You), pin: true);
            c.Unwant(new NetPlayerId(You));
            Assert.AreEqual(1, c.TopicCount, "pinned conversation stays");
            for (int i = 0; i < RealtimeDmClient.MaxTopics + 10; i++)
            {
                now[0] += 1;
                c.Want(new NetPlayerId("cccccccc-0000-0000-0000-" + i.ToString("x12")));
            }
            Assert.AreEqual(RealtimeDmClient.MaxTopics, c.TopicCount);
        }

        [Test]
        public void Client_GuestHasNoIdentity_NeverConnects()
        {
            var now = new double[] { 0 };
            var s = new FakeSocket();
            var c = new RealtimeDmClient(s, () => now[0]);
            c.SetIdentity(default, null);
            c.Want(new NetPlayerId(You));
            c.Tick();
            Assert.AreEqual(0, s.Connects);
            Assert.AreEqual(0, c.TopicCount);
        }
    }
}
