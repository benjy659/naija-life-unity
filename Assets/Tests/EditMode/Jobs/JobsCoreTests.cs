// Edit Mode tests for the M4 jobs core (docs/specs/jobs-m4.md).
// Ted's team: Window > General > Test Runner > EditMode > Run All; post the
// result under qa/m4.3/. The pay numbers match tools/jobsim.js.

using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace NaijaLife.Jobs.Tests
{
    public class ShiftRulesTests
    {
        [Test]
        public void Level_Grows_Slowly()
        {
            Assert.AreEqual(1, ShiftRules.Level(0));
            Assert.AreEqual(1, ShiftRules.Level(24));
            Assert.AreEqual(2, ShiftRules.Level(25));
            Assert.AreEqual(3, ShiftRules.Level(100));
            Assert.AreEqual(10, ShiftRules.Level(2025));
            Assert.AreEqual(10, ShiftRules.Level(100000));
        }

        [Test]
        public void TripPay_Matches_Jobsim()
        {
            Assert.AreEqual(680, ShiftRules.TripPay(14, 4, 1));
            Assert.AreEqual(760, ShiftRules.TripPay(14, 4, 2));
            Assert.AreEqual(850, ShiftRules.TripPay(14, 4, 3));
            Assert.AreEqual(1030, ShiftRules.TripPay(14, 4, 5));
            Assert.AreEqual(1470, ShiftRules.TripPay(14, 4, 10));
            Assert.AreEqual(4080, ShiftRules.ShiftCeiling(1));
            Assert.AreEqual(8820, ShiftRules.ShiftCeiling(10));
        }

        [Test]
        public void TripPay_Edges()
        {
            Assert.AreEqual(0, ShiftRules.TripPay(3, 4, 1), "under 4 seats pays nothing");
            Assert.AreEqual(0, ShiftRules.TripPay(0, 0, 10));
            // 12 seats, 3 of 4 right, level 1: (720 - 100) * 0.65 = 403 -> 400
            Assert.AreEqual(400, ShiftRules.TripPay(12, 3, 1));
            // 4 seats, none right: (240 - 400) -> floor 0
            Assert.AreEqual(0, ShiftRules.TripPay(4, 0, 1));
        }

        [Test]
        public void WatDay_Is_Utc_Plus_One()
        {
            Assert.AreEqual(new DateTime(2026, 11, 6), ShiftRules.WatDay(new DateTime(2026, 11, 5, 23, 30, 0, DateTimeKind.Utc)));
            Assert.AreEqual(new DateTime(2026, 11, 5), ShiftRules.WatDay(new DateTime(2026, 11, 5, 22, 59, 0, DateTimeKind.Utc)));
        }
    }

    public class ShiftLedgerTests
    {
        DateTime _now;
        ShiftLedger _l;
        int _ref;

        [SetUp]
        public void SetUp()
        {
            _now = new DateTime(2026, 11, 6, 9, 0, 0, DateTimeKind.Utc);
            _l = new ShiftLedger(() => _now, 8000);
            _ref = 0;
        }

        string Ref() => "r" + (++_ref);
        void Wait(double seconds) => _now = _now.AddSeconds(seconds);

        string StartOk()
        {
            ShiftStart s = _l.Start("conductor", "oshodi-bay-1-conductor", 80, Ref());
            Assert.IsTrue(s.Ok, s.Error);
            return s.ShiftId;
        }

        void Trips(string id, int n, int seats = 14, int right = 4)
        {
            for (int i = 0; i < n; i++)
            {
                Wait(160);
                TripResult t = _l.RecordTrip(id, seats, right, 4, Ref());
                Assert.IsTrue(t.Ok, t.Error);
            }
        }

        [Test]
        public void Full_Shift_Pays_Once_Into_Balance()
        {
            string id = StartOk();
            Trips(id, 5);
            ShiftEnd e = _l.End(id, "done");
            Assert.IsTrue(e.Ok);
            Assert.IsFalse(e.Voided);
            Assert.AreEqual(5 * 680, e.Paid);
            Assert.AreEqual(8000 + 5 * 680, e.BalanceAfter);
            ShiftEnd again = _l.End(id, "done");
            Assert.AreEqual(e.BalanceAfter, again.BalanceAfter, "ending twice never pays twice");
            Assert.AreEqual(8000 + 5 * 680, _l.Balance);
        }

        [Test]
        public void Pawa_Gate()
        {
            Assert.AreEqual(ShiftError.PawaLow, _l.Start("conductor", "s", 24, Ref()).Error);
            Assert.IsTrue(_l.Start("conductor", "s", 25, Ref()).Ok);
        }

        [Test]
        public void One_Open_Shift_At_A_Time()
        {
            StartOk();
            Assert.AreEqual(ShiftError.Busy, _l.Start("conductor", "s2", 80, Ref()).Error);
        }

        [Test]
        public void Same_Ref_Returns_Same_Shift()
        {
            ShiftStart a = _l.Start("conductor", "s", 80, "same");
            ShiftStart b = _l.Start("conductor", "s", 80, "same");
            Assert.AreEqual(a.ShiftId, b.ShiftId);
        }

        [Test]
        public void Trip_Too_Fast()
        {
            string id = StartOk();
            Wait(100);
            Assert.AreEqual(ShiftError.TooFast, _l.RecordTrip(id, 14, 4, 4, Ref()).Error);
            Wait(60);
            Assert.IsTrue(_l.RecordTrip(id, 14, 4, 4, Ref()).Ok, "150 s after clock in is fine");
        }

        [Test]
        public void Seventh_Trip_Is_Too_Many()
        {
            string id = StartOk();
            Trips(id, 6);           // 6 x 160 s = 16 min
            Wait(160);
            Assert.AreEqual(ShiftError.TooMany, _l.RecordTrip(id, 14, 4, 4, Ref()).Error);
        }

        [Test]
        public void Trip_Retry_Is_Not_Counted_Twice()
        {
            string id = StartOk();
            Wait(160);
            TripResult a = _l.RecordTrip(id, 14, 4, 4, "trip-1");
            TripResult b = _l.RecordTrip(id, 14, 4, 4, "trip-1");
            Assert.AreEqual(a.TripNo, b.TripNo);
            Assert.AreEqual(680, b.ShiftPending);
        }

        [Test]
        public void Bad_Input_Rejected()
        {
            string id = StartOk();
            Wait(160);
            Assert.AreEqual(ShiftError.BadInput, _l.RecordTrip(id, 15, 4, 4, Ref()).Error);
            Assert.AreEqual(ShiftError.BadInput, _l.RecordTrip(id, 14, 5, 4, Ref()).Error);
            Assert.AreEqual(ShiftError.BadInput, _l.RecordTrip(id, 14, 2, 3, Ref()).Error);
        }

        [Test]
        public void Short_Shift_Is_Void_And_Does_Not_Use_A_Daily_Shift()
        {
            string id = StartOk();
            Trips(id, 2);
            ShiftEnd e = _l.End(id, "left");
            Assert.IsTrue(e.Voided);
            Assert.AreEqual(0, e.Paid);
            Assert.AreEqual(8000, _l.Balance);
            for (int i = 0; i < 3; i++) { string s = StartOk(); Trips(s, 4); Assert.IsFalse(_l.End(s, "done").Voided); }
            Assert.AreEqual(ShiftError.CapReached, _l.Start("conductor", "s", 80, Ref()).Error);
        }

        [Test]
        public void Three_Shifts_Then_Next_Wat_Day_Opens_Again()
        {
            for (int i = 0; i < 3; i++) { string s = StartOk(); Trips(s, 4); _l.End(s, "done"); }
            Assert.AreEqual(ShiftError.CapReached, _l.Start("conductor", "s", 80, Ref()).Error);
            _now = new DateTime(2026, 11, 6, 23, 1, 0, DateTimeKind.Utc);   // 00:01 WAT on the 7th
            Assert.IsTrue(_l.Start("conductor", "s", 80, Ref()).Ok);
        }

        [Test]
        public void Stale_Open_Shift_Is_Closed_Lazily()
        {
            string id = StartOk();
            Trips(id, 3);           // last trip at +480 s, under 10 minutes from start
            Wait(25 * 60);
            ShiftStart next = _l.Start("conductor", "s", 80, Ref());
            Assert.IsTrue(next.Ok, "the stale shift closed, so a new one can start");
            Assert.IsTrue(_l.End(id, "x").Voided, "closed at its last trip: 480 s is under 10 minutes");
        }

        [Test]
        public void No_Trip_After_Hard_Stop()
        {
            string id = StartOk();
            Trips(id, 3);           // +8 min
            Wait(13 * 60);          // +21 min: past the 20 min stop
            Assert.AreEqual(ShiftError.NotOpen, _l.RecordTrip(id, 14, 4, 4, Ref()).Error);
        }

        [Test]
        public void Cheater_Cannot_Beat_The_Daily_Ceiling()
        {
            long start = _l.Balance;
            for (int i = 0; i < 5; i++) { ShiftStart s = _l.Start("conductor", "s", 100, Ref()); if (!s.Ok) break; Trips(s.ShiftId, 6); Wait(60); _l.End(s.ShiftId, "done"); }
            Assert.LessOrEqual(_l.Balance - start, 3 * ShiftRules.ShiftCeiling(1) + 3 * ShiftRules.ShiftCeiling(2));
        }
    }

    public class JobSlotBookTests
    {
        static Func<CancellationToken, Task<ClaimResult>> Pass => _ => Task.FromResult(ClaimResult.Ok);

        JobSlotBook Book(out SlotInfo a, out SlotInfo b, IJobSlotSync sync = null)
        {
            var book = new JobSlotBook(sync);
            a = new SlotInfo { Id = "bay-1", Kind = JobKind.Conductor, X = 0, Z = 0 };
            b = new SlotInfo { Id = "bay-2", Kind = JobKind.Conductor, X = 10, Z = 0 };
            book.Register(a); book.Register(b);
            return book;
        }

        [Test]
        public void Claim_Then_Release_Returns_To_Bot()
        {
            JobSlotBook book = Book(out SlotInfo a, out _);
            Assert.AreEqual(ClaimResult.Ok, book.TryClaimAsync("bay-1", 1, 1, Pass, CancellationToken.None).Result);
            Assert.AreEqual(SlotHolder.Local, a.Holder);
            Assert.AreSame(a, book.Held);
            ReleaseReason? why = null;
            book.Released += (s, r) => why = r;
            book.Release("bay-1", ReleaseReason.WalkedAway);
            Assert.AreEqual(SlotHolder.Bot, a.Holder);
            Assert.IsNull(book.Held);
            Assert.AreEqual(ReleaseReason.WalkedAway, why);
        }

        [Test]
        public void One_Job_At_A_Time()
        {
            JobSlotBook book = Book(out _, out SlotInfo b);
            book.TryClaimAsync("bay-1", 0, 0, Pass, CancellationToken.None).Wait();
            Assert.AreEqual(ClaimResult.Busy, book.TryClaimAsync("bay-2", 10, 0, Pass, CancellationToken.None).Result);
            Assert.AreEqual(SlotHolder.Bot, b.Holder);
            Assert.IsNull(book.NearestFree(JobKind.Conductor, 10, 0, 3f), "no offers while holding a job");
        }

        [Test]
        public void Too_Far_And_Gate_Refusals_Change_Nothing()
        {
            JobSlotBook book = Book(out SlotInfo a, out _);
            Assert.AreEqual(ClaimResult.TooFar, book.TryClaimAsync("bay-1", 5, 0, Pass, CancellationToken.None).Result);
            Assert.AreEqual(ClaimResult.NoPawa, book.TryClaimAsync("bay-1", 0, 0, _ => Task.FromResult(ClaimResult.NoPawa), CancellationToken.None).Result);
            Assert.AreEqual(SlotHolder.Bot, a.Holder);
            Assert.IsNull(book.Held);
        }

        [Test]
        public void Nearest_Free_Skips_Remote()
        {
            var sync = new FakeSync();
            JobSlotBook book = Book(out SlotInfo a, out SlotInfo b, sync);
            Assert.AreSame(a, book.NearestFree(JobKind.Conductor, 1, 0, 20f));
            sync.Raise("bay-1", true);
            Assert.AreEqual(SlotHolder.Remote, a.Holder);
            Assert.AreSame(b, book.NearestFree(JobKind.Conductor, 1, 0, 20f));
            Assert.AreEqual(ClaimResult.Taken, book.TryClaimAsync("bay-1", 0, 0, Pass, CancellationToken.None).Result);
            sync.Raise("bay-1", false);
            Assert.AreEqual(SlotHolder.Bot, a.Holder);
        }

        [Test]
        public void Room_Says_No_Means_Taken()
        {
            var sync = new FakeSync { Answer = false };
            JobSlotBook book = Book(out SlotInfo a, out _, sync);
            Assert.AreEqual(ClaimResult.Taken, book.TryClaimAsync("bay-1", 0, 0, Pass, CancellationToken.None).Result);
            Assert.AreEqual(SlotHolder.Bot, a.Holder);
        }

        [Test]
        public void Duplicate_Ids_Refused()
        {
            var book = new JobSlotBook();
            book.Register(new SlotInfo { Id = "x" });
            Assert.Throws<ArgumentException>(() => book.Register(new SlotInfo { Id = "x" }));
        }

        sealed class FakeSync : IJobSlotSync
        {
            public bool Answer = true;
            public event Action<string, bool> RemoteChanged;
            public Task<bool> RequestClaimAsync(string slotId, CancellationToken cancel) => Task.FromResult(Answer);
            public void Release(string slotId) { }
            public void Raise(string id, bool held) => RemoteChanged?.Invoke(id, held);
        }
    }

    public class ConductorMinigameTests
    {
        [Test]
        public void Shout_Gold_Tap_Fills_A_Seat_Early_Tap_Locks()
        {
            var r = new ShoutRound(1);
            // period 1.335 s at level 1; scale hits 1.0 at 0.625 of the period
            double gold = ShiftRules.RingPeriodSeconds(1) * 0.625;
            r.Tick(0.05);
            Assert.AreEqual(ShoutRound.Tap.Early, r.Shout());
            Assert.AreEqual(ShoutRound.Tap.Ignored, r.Shout(), "locked for 450 ms after an early tap");
            r.Tick(gold - 0.05);
            Assert.IsTrue(r.IsGold);
            Assert.AreEqual(ShoutRound.Tap.Hit, r.Shout());
            Assert.AreEqual(1, r.Seats);
        }

        [Test]
        public void Shout_Ends_At_45_Seconds()
        {
            var r = new ShoutRound(1);
            for (int i = 0; i < 1000; i++) r.Tick(0.05);
            Assert.IsTrue(r.Finished);
            Assert.IsFalse(r.Departs);
        }

        [Test]
        public void Change_Has_One_Right_Answer()
        {
            var rng = new Random(7);
            for (int i = 0; i < 200; i++)
            {
                ChangeQuestion q = ChangeQuestion.Make(rng);
                Assert.AreEqual(q.Note - ShiftRules.Fare, q.Correct);
                Assert.AreEqual(3, q.Options.Length);
                int hits = 0;
                foreach (int o in q.Options) { Assert.Greater(o, 0); if (o == q.Correct) hits++; }
                Assert.AreEqual(1, hits);
                Assert.AreNotEqual(q.Options[0], q.Options[1]);
                Assert.AreNotEqual(q.Options[1], q.Options[2]);
                Assert.AreNotEqual(q.Options[0], q.Options[2]);
            }
        }

        [Test]
        public void Release_Rules()
        {
            var keep = new ReleaseInput { DistanceFromWork = 3, IdleSeconds = 10, ShiftSecondsLeft = 300, Pawa = 50 };
            Assert.IsNull(ReleaseWatch.Check(keep));
            var far = keep; far.DistanceFromWork = 9;
            Assert.AreEqual(ReleaseReason.WalkedAway, ReleaseWatch.Check(far));
            var idle = keep; idle.IdleSeconds = 181;
            Assert.AreEqual(ReleaseReason.Idle, ReleaseWatch.Check(idle));
            var riding = far; riding.OnTrip = true; riding.EndRequested = true;
            Assert.IsNull(ReleaseWatch.Check(riding), "finish the trip first");
            var tired = riding; tired.Pawa = 0;
            Assert.AreEqual(ReleaseReason.PawaEmpty, ReleaseWatch.Check(tired));
        }

        [Test]
        public void Pawa_Drains_About_18_A_Shift()
        {
            var p = new PawaMeter(100);
            p.DrainWorking(15 * 60);
            Assert.AreEqual(82f, p.Value, 0.01f);
            p.Set(24.9f);
            Assert.IsFalse(p.CanClockIn);
        }
    }
}
