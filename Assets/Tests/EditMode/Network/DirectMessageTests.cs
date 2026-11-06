// Edit Mode tests for the M5.3 DM topic and friend error helpers (Core only).
// The topic rules mirror the live RLS function nl_rt_dm_ok (checked 2026-11-06).

using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace NaijaLife.Network.Tests
{
    public class DirectMessageTests
    {
        const string A = "11111111-2222-3333-4444-555555555555";
        const string B = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";

        [Test]
        public void Topic_IsSorted_SameFromBothSides()
        {
            string t1 = DmTopic.For(new NetPlayerId(A), new NetPlayerId(B));
            string t2 = DmTopic.For(new NetPlayerId(B), new NetPlayerId(A));
            Assert.AreEqual("d:" + A + ":" + B, t1);
            Assert.AreEqual(t1, t2);
        }

        [Test]
        public void Topic_LowercasesIds()
        {
            Assert.AreEqual("d:" + A + ":" + B, DmTopic.For(new NetPlayerId(A), new NetPlayerId(B.ToUpperInvariant())));
        }

        [Test]
        public void Topic_RefusesSelfAndBadIds()
        {
            Assert.IsNull(DmTopic.For(new NetPlayerId(A), new NetPlayerId(A)));
            Assert.IsNull(DmTopic.For(new NetPlayerId(A), new NetPlayerId("not-a-uuid")));
            Assert.IsNull(DmTopic.For(new NetPlayerId(A), default));
            Assert.IsNull(DmTopic.For(new NetPlayerId(A), new NetPlayerId("11111111x2222-3333-4444-555555555555")));
        }

        [Test]
        public void Other_IsTheOtherSide_OnlyForMyTopics()
        {
            string t = "d:" + A + ":" + B;
            Assert.IsTrue(DmTopic.TryOther(t, new NetPlayerId(A), out NetPlayerId o1));
            Assert.AreEqual(new NetPlayerId(B), o1);
            Assert.IsTrue(DmTopic.TryOther(t, new NetPlayerId(B), out NetPlayerId o2));
            Assert.AreEqual(new NetPlayerId(A), o2);
            Assert.IsFalse(DmTopic.TryOther(t, new NetPlayerId("99999999-2222-3333-4444-555555555555"), out _));
        }

        [Test]
        public void Other_RefusesUnsortedOrMalformedTopics()
        {
            Assert.IsFalse(DmTopic.TryOther("d:" + B + ":" + A, new NetPlayerId(A), out _)); // server refuses lo >= hi too
            Assert.IsFalse(DmTopic.TryOther("d:" + A + ":" + A, new NetPlayerId(A), out _));
            Assert.IsFalse(DmTopic.TryOther("x:" + A + ":" + B, new NetPlayerId(A), out _));
            Assert.IsFalse(DmTopic.TryOther(null, new NetPlayerId(A), out _));
        }

        [Test]
        public void FriendErrors_MapLiveCodes()
        {
            Assert.AreEqual(FriendError.None, FriendErrors.Parse(null));
            Assert.AreEqual(FriendError.Denied, FriendErrors.Parse("denied"));
            Assert.AreEqual(FriendError.FriendCap, FriendErrors.Parse("friend_cap"));
            Assert.AreEqual(FriendError.NoPending, FriendErrors.Parse("no_pending"));
            Assert.AreEqual(FriendError.Unknown, FriendErrors.Parse("something_new"));
            foreach (FriendError e in System.Enum.GetValues(typeof(FriendError)))
                Assert.IsFalse(string.IsNullOrEmpty(FriendErrors.Message(e)));
        }

        static Task<bool> SendOk(NetPlayerId to, string line, CancellationToken c) => Task.FromResult(true);

        [Test]
        public void Chat_DirectGoesThroughSender_WhenWired()
        {
            double now = 0;
            var chat = new ChatCore(t => true, SendOk, () => now, null);
            Assert.AreEqual(ChatRefusal.None, chat.SendDirectAsync(new NetPlayerId(B), "how far", CancellationToken.None).Result);
            var offline = new ChatCore(t => true, null, () => now, null);
            Assert.AreEqual(ChatRefusal.Offline, offline.SendDirectAsync(new NetPlayerId(B), "how far", CancellationToken.None).Result);
        }
    }
}
