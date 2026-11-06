// Edit Mode tests for the M5 netcode core (docs/specs/netcode-m5.md).
// Ted's team: Window > General > Test Runner > EditMode > Run All. All must pass (M5.1c).
// The same scenarios are checked in tools/netsim.js (node) so the maths was
// checked before Unity was available. UNVERIFIED in Unity until the run is posted.

using System.Collections.Generic;
using NUnit.Framework;

namespace NaijaLife.Network.Tests
{
    public class NetCoreTests
    {
        static NetPose Walk(float x, float z, float heading = 0f) =>
            new NetPose { X = x, Z = z, HeadingDeg = heading, Speed = 1.4f, Flags = MoveFlags.Moving };
        static NetPose Drive(float x, float z) =>
            new NetPose { X = x, Z = z, Speed = 12f, Flags = MoveFlags.Moving | MoveFlags.Driving, VehicleModelId = 3 };
        static NetPose Still(float x, float z) => new NetPose { X = x, Z = z };
        static bool SendOk(in NetPose p, ushort s) => true; // method group: older compilers mis-read `in` lambdas across assemblies

        // ---- MoveCodec ----

        [Test]
        public void Codec_RoundTrips_Within_Precision()
        {
            var p = new NetPose { X = -1062.37f, Y = 12.34f, Z = -822.5f, HeadingDeg = 271.3f, Speed = 6.45f,
                                  Flags = MoveFlags.Moving | MoveFlags.Running, AnimId = 9, VehicleModelId = 0 };
            var buf = new byte[MoveCodec.Size];
            MoveCodec.Encode(p, 65535, buf);
            Assert.IsTrue(MoveCodec.TryDecode(buf, 0, buf.Length, out NetPose q, out ushort seq));
            Assert.AreEqual(65535, seq);
            Assert.AreEqual(p.X, q.X, 1e-4f);
            Assert.AreEqual(p.Z, q.Z, 1e-4f);
            Assert.AreEqual(p.Y, q.Y, 0.006f);
            Assert.AreEqual(p.HeadingDeg, q.HeadingDeg, 0.01f);
            Assert.AreEqual(p.Speed, q.Speed, 0.006f);
            Assert.AreEqual(p.Flags, q.Flags);
            Assert.AreEqual(9, q.AnimId);
        }

        [Test]
        public void Codec_Rejects_Bad_Input()
        {
            var buf = new byte[MoveCodec.Size];
            MoveCodec.Encode(Walk(1, 2), 1, buf);
            Assert.IsFalse(MoveCodec.TryDecode(buf, 0, 19, out _, out _), "wrong length");
            buf[0] = 2;
            Assert.IsFalse(MoveCodec.TryDecode(buf, 0, 20, out _, out _), "wrong version");
            MoveCodec.Encode(new NetPose { X = float.NaN }, 1, buf);
            Assert.IsFalse(MoveCodec.TryDecode(buf, 0, 20, out _, out _), "NaN");
        }

        [Test]
        public void Sequence_Is_Wrap_Aware()
        {
            Assert.IsTrue(MoveCodec.IsNewer(0, 65535));
            Assert.IsTrue(MoveCodec.IsNewer(5, 2));
            Assert.IsFalse(MoveCodec.IsNewer(2, 5));
            Assert.IsFalse(MoveCodec.IsNewer(7, 7));
        }

        // ---- SendPolicy ----

        static int CountSends(SendPolicy sp, System.Func<double, NetPose> poseAt, double seconds, double fps = 60)
        {
            int n = 0;
            for (int i = 0; i <= seconds * fps; i++) { double t = i / fps; if (sp.TryConsume(poseAt(t), t)) n++; }
            return n;
        }

        [Test]
        public void Walking_Sends_About_2Hz()
        {
            int n = CountSends(new SendPolicy(), t => Walk((float)(t * 1.4), 0), 10);
            Assert.That(n, Is.InRange(19, 22)); // first + ~2/s
        }

        [Test]
        public void Driving_Sends_About_4Hz()
        {
            int n = CountSends(new SendPolicy(), t => Drive((float)(t * 12), 0), 10);
            Assert.That(n, Is.InRange(39, 42));
        }

        [Test]
        public void Standing_Sends_KeepAlive_Every_5s()
        {
            int n = CountSends(new SendPolicy(), t => Still(5, 5), 20);
            Assert.AreEqual(5, n); // t=0,5,10,15,20
        }

        [Test]
        public void Moving_Flag_But_No_Change_Counts_As_Standing()
        {
            int n = CountSends(new SendPolicy(), t => Walk(5, 5), 20);
            Assert.AreEqual(5, n);
        }

        [Test]
        public void Stop_And_Start_Send_At_Once()
        {
            var sp = new SendPolicy();
            Assert.IsTrue(sp.TryConsume(Walk(0, 0), 0));
            Assert.IsFalse(sp.TryConsume(Walk(0.1f, 0), 0.1));
            Assert.IsTrue(sp.TryConsume(Still(0.15f, 0), 0.15), "stop goes at once");
            Assert.IsTrue(sp.TryConsume(Walk(0.16f, 0), 0.2), "start goes at once");
            Assert.IsTrue(sp.TryConsume(Drive(0.2f, 0), 0.25), "entering a vehicle goes at once");
        }

        [Test]
        public void Paused_Sends_Nothing_Then_Resends()
        {
            var sp = new SendPolicy();
            Assert.IsTrue(sp.TryConsume(Walk(0, 0), 0));
            sp.SetPaused(true);
            Assert.IsFalse(sp.TryConsume(Walk(5, 0), 10));
            sp.SetPaused(false);
            Assert.IsTrue(sp.TryConsume(Walk(5, 0), 10.01));
        }

        // ---- SnapshotBuffer ----

        [Test]
        public void Walker_Is_Drawn_600ms_Behind()
        {
            var b = new SnapshotBuffer();
            for (int i = 0; i <= 10; i++) b.Add(Walk(i * 0.7f, 0), (ushort)(i + 1), i * 0.5); // 1.4 m/s
            Assert.IsTrue(b.TrySample(5.0, out NetPose p));
            Assert.AreEqual((5.0 - 0.6) * 1.4, p.X, 0.02);
        }

        [Test]
        public void Driver_Delay_Slides_Not_Jumps()
        {
            var b = new SnapshotBuffer();
            b.Add(Walk(0, 0), 1, 0);
            b.TrySample(0.1, out _);
            Assert.AreEqual(0.6, b.CurrentDelay, 1e-9);
            b.Add(Drive(1, 0), 2, 0.25);
            b.TrySample(0.6, out _); // 0.5 s later: slid by 0.15
            Assert.AreEqual(0.45, b.CurrentDelay, 1e-6);
            b.TrySample(5.0, out _);
            Assert.AreEqual(0.3, b.CurrentDelay, 1e-9);
        }

        [Test]
        public void Impossible_Speed_Is_Dropped_And_Hides_After_Six()
        {
            var b = new SnapshotBuffer();
            Assert.IsTrue(b.Add(Walk(0, 0), 1, 0));
            for (int i = 0; i < 6; i++) Assert.IsFalse(b.Add(Walk(20 + i, 0), (ushort)(2 + i), 0.5 + i * 0.1)); // 20+ m in about 1 s on foot
            Assert.IsTrue(b.IsHidden);
            Assert.IsFalse(b.TrySample(4, out _));
            Assert.IsTrue(b.Add(Walk(0.5f, 0), 20, 4), "next good snapshot shows the player again");
            Assert.IsFalse(b.IsHidden);
        }

        [Test]
        public void Bunched_Packets_Are_Not_Treated_As_Fast()
        {
            var b = new SnapshotBuffer();
            b.Add(Walk(0, 0), 1, 0);
            Assert.IsTrue(b.Add(Walk(3.25f, 0), 2, 0.001)); // sprint 6.5 m/s, arrived late in a bunch
        }

        [Test]
        public void Jump_Over_30m_Snaps()
        {
            var b = new SnapshotBuffer();
            b.Add(Walk(0, 0), 1, 0);
            b.Add(Walk(0.7f, 0), 2, 0.5);
            Assert.IsTrue(b.Add(Drive(100, 0), 3, 1.0));
            Assert.IsTrue(b.TrySample(1.1, out NetPose p));
            Assert.AreEqual(100f, p.X, 1e-3);
        }

        [Test]
        public void Extrapolates_At_Most_250ms_Then_Holds()
        {
            var b = new SnapshotBuffer();
            b.Add(Walk(0, 0), 1, 0);
            b.Add(Walk(0.7f, 0), 2, 0.5); // 1.4 m/s; last packet at t=0.5
            Assert.IsTrue(b.TrySample(3.0, out NetPose p)); // render time 2.4, way past
            Assert.AreEqual(0.7 + 1.4 * 0.25, p.X, 0.01);
        }

        [Test]
        public void Old_Sequence_Is_Ignored()
        {
            var b = new SnapshotBuffer();
            Assert.IsTrue(b.Add(Walk(0, 0), 10, 0));
            Assert.IsFalse(b.Add(Walk(0.1f, 0), 9, 0.5));
            Assert.IsFalse(b.IsHidden);
        }

        [Test]
        public void Heading_Takes_The_Short_Way()
        {
            NetPose p = SnapshotBuffer.Lerp(Walk(0, 0, 350f), Walk(0, 0, 10f), 0.5f);
            Assert.AreEqual(0f, p.HeadingDeg % 360f, 0.01f);
        }

        // ---- Interest ----

        [Test]
        public void Cells_Ring_Of_Nine_And_Delayed_Leave()
        {
            var ci = new CellInterest();
            ci.Update(100, 100, 0);
            Assert.AreEqual(9, ci.Count);
            Assert.IsTrue(ci.Contains(-150, -150));
            ci.Update(250, 100, 1); // into cell (1,0): column x=-1 starts its 3 s countdown
            Assert.AreEqual(12, ci.Count);
            ci.Update(250, 100, 3.9);
            Assert.AreEqual(12, ci.Count);
            ci.Update(250, 100, 4.0);
            Assert.AreEqual(9, ci.Count);
        }

        [Test]
        public void Back_And_Forth_Across_A_Border_Does_Not_Churn()
        {
            var ci = new CellInterest();
            ci.Update(199, 100, 0);
            ci.Update(201, 100, 0.5);
            ci.Update(199, 100, 1.0);
            ci.Update(201, 100, 1.5);
            Assert.AreEqual(12, ci.Count);
        }

        [Test]
        public void Nearby_Keeps_40_Nearest()
        {
            var list = new List<KeyValuePair<NetPlayerId, NetPose>>();
            for (int i = 0; i < 70; i++) list.Add(new KeyValuePair<NetPlayerId, NetPose>(new NetPlayerId("p" + i), Still(70 - i, 0)));
            var into = new List<NetPlayerId>();
            NearbyPicker.Pick(list, 0, 0, NetLimits.MaxNearby, into);
            Assert.AreEqual(40, into.Count);
            Assert.AreEqual("p69", into[0].Value);
        }

        // ---- Limits ----

        [Test]
        public void Backoff_Caps_At_30s()
        {
            Assert.AreEqual(1.0, Backoff.Cap(0));
            Assert.AreEqual(16.0, Backoff.Cap(4));
            Assert.AreEqual(30.0, Backoff.Cap(5));
            Assert.AreEqual(30.0, Backoff.Cap(500));
            Assert.AreEqual(15.0, Backoff.Delay(9, 0.5));
        }

        [Test]
        public void Events_Limited_To_2_Per_Second()
        {
            double now = 0;
            int sent = 0;
            var ev = new WorldEventsCore(e => { sent++; return true; }, () => now);
            for (int i = 0; i < 10; i++) ev.Raise(WorldEventKind.Wave, 0);
            Assert.AreEqual(2, sent);
            now = 1.0;
            Assert.IsTrue(ev.Raise(WorldEventKind.Horn, 0));
        }

        [Test]
        public void Remote_Event_Spam_Is_Dropped()
        {
            double now = 0;
            int got = 0;
            var ev = new WorldEventsCore(e => true, () => now);
            ev.Received += (from, e) => got++;
            for (int i = 0; i < 10; i++) ev.OnRemote(new NetPlayerId("a"), (byte)WorldEventKind.Wave, 0);
            ev.OnRemote(new NetPlayerId("b"), (byte)WorldEventKind.Wave, 0);
            ev.OnRemote(new NetPlayerId("b"), 99, 0); // unknown kind
            Assert.AreEqual(3, got);
        }

        [Test]
        public void Chat_Rules()
        {
            double now = 0;
            var sent = new List<string>();
            var chat = new ChatCore(t => { sent.Add(t); return true; }, null, () => now, new[] { "badword" });
            Assert.AreEqual(ChatRefusal.Empty, chat.SendLobby("   "));
            Assert.AreEqual(ChatRefusal.TooLong, chat.SendLobby(new string('a', 201)));
            Assert.AreEqual(ChatRefusal.Blocked, chat.SendLobby("you b.a.d word"));
            Assert.AreEqual(ChatRefusal.None, chat.SendLobby("  how   far\n"));
            Assert.AreEqual("how far", sent[0]);
            Assert.AreEqual(ChatRefusal.TooFast, chat.SendLobby("again"));
            Assert.AreEqual("<noparse><b>hi</b></noparse>", ChatRules.EscapeForRichText("<b>hi</b>"));
        }

        [Test]
        public void Offline_Session_Has_No_Players_And_Sends_Nothing()
        {
            var s = new OfflineSession(() => 0);
            Assert.AreEqual(NetStatus.Offline, s.Status);
            Assert.AreEqual(0, s.RemotePlayers.Count);
            Assert.IsFalse(s.Events.Raise(WorldEventKind.Wave, 0));
            Assert.AreEqual(ChatRefusal.Offline, s.Chat.SendLobby("hello"));
            var near = new List<NetPlayerId>();
            s.Players.GetNearby(0, 0, near);
            Assert.AreEqual(0, near.Count);
        }

        [Test]
        public void Public_State_At_Most_Every_5s_Latest_Wins()
        {
            int sends = 0;
            PublicState last = default;
            var r = new ReplicatorCore(SendOk, st => { sends++; last = st; return true; });
            r.SetLocalPublicState(new PublicState { AppearanceId = 1 });
            r.SubmitLocal(Still(0, 0), 0);
            r.SetLocalPublicState(new PublicState { AppearanceId = 2 });
            r.SubmitLocal(Still(0, 0), 1);
            r.SetLocalPublicState(new PublicState { AppearanceId = 3 });
            r.SubmitLocal(Still(0, 0), 4.9);
            Assert.AreEqual(1, sends);
            r.SubmitLocal(Still(0, 0), 5.0);
            Assert.AreEqual(2, sends);
            Assert.AreEqual(3, last.AppearanceId);
        }

        [Test]
        public void StreetCard_Cached_10_Minutes()
        {
            double now = 0;
            int calls = 0;
            var cache = new StreetCardCache((who, ct) =>
            {
                calls++;
                return System.Threading.Tasks.Task.FromResult(new StreetCard { Username = "ada", Id = new NetPlayerId("spoof") });
            }, () => now);
            var id = new NetPlayerId("u1");
            StreetCard c = cache.GetAsync(id, default).Result;
            Assert.AreEqual("u1", c.Id.Value, "id comes from the caller, not the payload");
            cache.GetAsync(id, default).Wait();
            now = 599;
            cache.GetAsync(id, default).Wait();
            Assert.AreEqual(1, calls);
            now = 601;
            cache.GetAsync(id, default).Wait();
            Assert.AreEqual(2, calls);
        }
    }
}
