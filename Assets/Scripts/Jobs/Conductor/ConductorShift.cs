// Naija Streets (Unity) - the danfo conductor shift, end to end
// (docs/specs/jobs-m4.md section 6). Clock in through the server (or the
// Editor stub), load the bus by shouting, ride the route at the door while
// collecting fares, drop off, ride back, record the trip, repeat, clock out.
// The server prices every trip and pays once at clock out; this script only
// shows what the server answered. Lives on the player (PlayerJobs adds it).

using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using NaijaLife.Economy;
using NaijaLife.Jobs;
using NaijaLife.Player;
using UnityEngine;

namespace NaijaLife.JobsGlue
{
    public sealed class ConductorShift : MonoBehaviour
    {
        const string ExitId = "job.end";
        const float FaresStartDelay = 5f;
        const float BetweenFares = 1.2f;
        const float StepDownSeconds = 0.4f;
        static readonly string[] Shouts = { "Yaba! Yaba!", "Ojuelegba straight!", "CMS! CMS!", "Ikeja along!", "Oshodi oke!", "Enter with your change!", "Two chance remain!", "Obalende! Obalende!" };

        PlayerJobs _jobs;
        IShiftService _svc;
        ConductorBay _bay;
        string _shiftId;
        DateTime _endsByUtc;
        int _trips;
        long _pending;
        float _idle;
        bool _onTrip, _endRequested, _ending;
        CancellationTokenSource _cts;
        readonly System.Random _rng = new System.Random();

        /// <summary>True from the clock-in tap until the shift is fully closed.</summary>
        public bool Busy { get; private set; }

        void Awake() { _jobs = GetComponent<PlayerJobs>(); }

        void OnDestroy() { _cts?.Cancel(); }

        public void Begin(ConductorBay bay)
        {
            if (Busy || !bay) return;
            IJobUi ui = _jobs.Ui;
            PawaMeter pawa = _jobs.Needs.Pawa;
            if (!pawa.CanClockIn) { ui.Toast("Pawa too low", pawa.RefuseText); return; }
            if (!bay.AtBay) { ui.Toast("Wait small", "The bus never come back from the trip."); return; }
            Busy = true;
            _bay = bay;
            _svc = ShiftServiceFactory.Current;
            _cts = new CancellationTokenSource();
            StartCoroutine(ClockIn());
        }

        IEnumerator ClockIn()
        {
            IJobUi ui = _jobs.Ui;
            JobSlotBook book = JobSlotRegistry.Shared.Book;
            string startRef = Guid.NewGuid().ToString("N");
            _shiftId = null;
            ui.ShowBusy("Clocking in...");
            Vector3 p = transform.position;
            Task<ClaimResult> claim = book.TryClaimAsync(_bay.Slot.slotId, p.x, p.z, async c =>
            {
                ShiftStart s = await StartWithRetry(startRef, c);
                if (!s.Ok)
                {
                    string text = ShiftError.PlayerText(s.Error);
                    if (text != null) ui.Toast("Can't clock in", text);
                    return s.Error == ShiftError.PawaLow ? ClaimResult.NoPawa
                         : s.Error == ShiftError.CapReached ? ClaimResult.ShiftCap
                         : s.Error == ShiftError.Network ? ClaimResult.Offline : ClaimResult.Refused;
                }
                _shiftId = s.ShiftId;
                _endsByUtc = s.EndsByUtc;
                return ClaimResult.Ok;
            }, _cts.Token);
            while (!claim.IsCompleted) yield return null;
            ui.ShowBusy(null);

            ClaimResult result = claim.IsFaulted || claim.IsCanceled ? ClaimResult.Offline : claim.Result;
            if (result != ClaimResult.Ok)
            {
                if (result == ClaimResult.Taken) ui.Toast("Job taken", "Another person don take this bus.");
                if (_shiftId != null) yield return Await(_svc.EndShiftAsync(_shiftId, "claim_failed", _cts.Token));   // voids: no trips
                Busy = false;
                yield break;
            }
            if (_svc.IsTestStub) ui.Toast("Test shift", "Editor stub: money here is TEST only.");
            ui.Toast("You don clock in", "\"Oya, try am.\" Work 10 minutes to get paid.");
            _trips = 0; _pending = 0; _idle = 0f; _endRequested = false; _ending = false; _onTrip = false;
            ui.Offer(ExitId, PromptKind.Exit, "End shift", "Danfo conductor", null, RequestEnd);
            StartCoroutine(Watch());
            StartCoroutine(TripLoop());
        }

        async Task<ShiftStart> StartWithRetry(string clientRef, CancellationToken c)
        {
            ShiftStart s = default;
            for (int i = 0; i < 3; i++)
            {
                s = await _svc.StartShiftAsync(JobKind.Conductor, _bay.Slot.slotId, _jobs.Needs.Pawa.Reported, clientRef, c);
                if (s.Error != ShiftError.Network) return s;
                await Task.Delay(1000 * (i + 1), c);
            }
            return s;
        }

        void RequestEnd()
        {
            _endRequested = true;
            if (_onTrip) _jobs.Ui.Toast("Finish the trip", "We go finish this trip first.");
        }

        float SecondsLeft => (float)(_endsByUtc - DateTime.UtcNow).TotalSeconds;

        // ---- release watch (spec 4.3) and Pawa drain (spec 7) ----
        IEnumerator Watch()
        {
            while (!_ending)
            {
                _jobs.Needs.Pawa.DrainWorking(Time.deltaTime);
                if (!_onTrip) _idle += Time.deltaTime;
                Vector3 w = _bay.Slot.workPoint ? _bay.Slot.workPoint.position : _bay.transform.position;
                Vector3 d = transform.position - w; d.y = 0f;
                ReleaseReason? why = ReleaseWatch.Check(new ReleaseInput
                {
                    DistanceFromWork = d.magnitude,
                    OnTrip = _onTrip,
                    IdleSeconds = _idle,
                    ShiftSecondsLeft = SecondsLeft,
                    Pawa = _jobs.Needs.Pawa.Value,
                    EndRequested = _endRequested,
                });
                // Time up never cuts a loading round short of a trip already counted; it just stops new rounds.
                if (why.HasValue) { yield return EndShift(why.Value); yield break; }
                _jobs.Ui.ShowShiftChip(true, Mathf.Max(0f, SecondsLeft), _trips, _pending,
                    _trips < ShiftRules.MinPaidTrips ? "Work 10 minutes and 3 trips to get paid" : "Clock out any time to get paid");
                yield return null;
            }
        }

        // ---- one trip after another (spec 6.1) ----
        IEnumerator TripLoop()
        {
            IJobUi ui = _jobs.Ui;
            while (!_ending)
            {
                if (SecondsLeft <= 0f || _endRequested || _trips >= ShiftRules.MaxTripsPerShift) { yield return null; continue; }

                // 2. Load (shout).
                var round = new ShoutRound(Level());
                string say = "";
                Action shout = () =>
                {
                    ShoutRound.Tap t = round.Shout();
                    _idle = 0f;
                    if (t == ShoutRound.Tap.Hit) { if (_bay.queue) _bay.queue.CallOne(); say = Shouts[round.Seats % Shouts.Length]; }
                    else if (t == ShoutRound.Tap.Early) say = "Too early! Passengers dey look you.";
                };
                while (!round.Finished && !_ending)
                {
                    round.Tick(Time.deltaTime);
                    ui.ShowShout(new ShoutView { RingScale = (float)round.RingScale, Gold = round.IsGold, Seats = round.Seats, SeatsMax = ShiftRules.Seats, SecondsLeft = (float)round.SecondsLeft, Say = say }, shout);
                    yield return null;
                }
                ui.ShowShout(null, null);
                if (_ending) yield break;

                // 3. Depart, or the driver leaves without you.
                if (!round.Departs)
                {
                    ui.Toast("Driver don go", "\"I no fit wait again.\" Only " + round.Seats + " passengers. No pay.");
                    if (_bay.queue) _bay.queue.ResetBoarded();
                    float until = Time.time + ShiftRules.DriverLeftCooldownSeconds;
                    while (Time.time < until && !_ending) yield return null;
                    continue;
                }
                if (round.Seats >= ShiftRules.Seats) ui.Toast("Full bus!", "Driver, move!");

                // 4-7. Ride out at the door, collect fares, drop, ride back.
                _onTrip = true;
                yield return Board();
                int right = 0;
                Coroutine fares = StartCoroutine(CollectFares(r => right = r));
                if (_bay.route) yield return _bay.route.Drive(_bay.bus);
                else yield return new WaitForSeconds(160f);   // no route in this scene: wait like a real trip
                if (fares != null) StopCoroutine(fares);
                ui.ShowChange(null, null);
                if (_ending) yield break;   // shift ended during the ride (Pawa 0): EndShift already stepped down
                yield return StepDown();
                _onTrip = false;
                _idle = 0f;

                // 8. Trip recorded and priced by the server.
                yield return RecordTrip(round.Seats, right);
            }
        }

        IEnumerator CollectFares(Action<int> done)
        {
            yield return new WaitForSeconds(FaresStartDelay);
            int right = 0;
            for (int i = 0; i < ShiftRules.FaresPerTrip; i++)
            {
                ChangeQuestion q = ChangeQuestion.Make(_rng);
                int picked = -1;
                float t0 = Time.time;
                Action<int> pick = k => { if (picked < 0) picked = q.Options[k]; };
                while (picked < 0 && Time.time - t0 < ShiftRules.AnswerSeconds)
                {
                    _jobs.Ui.ShowChange(new ChangeView { Index = i, Of = ShiftRules.FaresPerTrip, Note = q.Note, Fare = ShiftRules.Fare, Options = q.Options, SecondsLeft = ShiftRules.AnswerSeconds - (Time.time - t0) }, pick);
                    yield return null;
                }
                string result = picked < 0 ? "Too slow! The passenger vex." : q.ResultText(picked);
                if (picked == q.Correct) right++;
                done(right);
                _jobs.Ui.ShowChange(new ChangeView { Index = i, Of = ShiftRules.FaresPerTrip, Note = q.Note, Fare = ShiftRules.Fare, Options = q.Options, Result = result }, null);
                yield return new WaitForSeconds(BetweenFares);
            }
            _jobs.Ui.ShowChange(null, null);
        }

        IEnumerator RecordTrip(int seats, int right)
        {
            string tripRef = Guid.NewGuid().ToString("N");
            TripResult r = default;
            for (int i = 0; i < 3; i++)
            {
                if (i > 0) _jobs.Ui.ShowBusy("Saving your trip...");
                Task<TripResult> t = _svc.RecordTripAsync(_shiftId, seats, right, ShiftRules.FaresPerTrip, tripRef, _cts.Token);
                yield return Await(t);
                r = t.IsCompleted && !t.IsFaulted && !t.IsCanceled ? t.Result : new TripResult { Error = ShiftError.Network };
                if (r.Error != ShiftError.Network) break;
                yield return new WaitForSeconds(1f + i);
            }
            _jobs.Ui.ShowBusy(null);
            if (r.Ok)
            {
                _trips = r.TripNo;
                _pending = r.ShiftPending;
                _jobs.Ui.Toast("Trip " + r.TripNo + " done", seats + " passengers, " + right + "/4 change right. +₦" + r.TripPay.ToString("N0") + " this shift");
            }
            else
            {
                string text = ShiftError.PlayerText(r.Error);
                if (text != null) _jobs.Ui.Toast("Trip not counted", text);
                else Debug.LogWarning("[Jobs] trip not counted: " + r.Error);
                if (r.Error == ShiftError.NotOpen) _endRequested = true;
            }
        }

        // ---- clock out (spec 6.1 step 10) ----
        IEnumerator EndShift(ReleaseReason why)
        {
            _ending = true;
            IJobUi ui = _jobs.Ui;
            ui.ShowShout(null, null);
            ui.ShowChange(null, null);
            ui.Withdraw(ExitId);
            string releaseText = ReleaseWatch.PlayerText(why);
            if (releaseText != null) ui.Toast("Shift over", releaseText);
            if (_onTrip) { yield return StepDown(); _onTrip = false; }

            ShiftEnd e = default;
            for (int i = 0; i < 3; i++)
            {
                ui.ShowBusy("Saving your pay...");
                Task<ShiftEnd> t = _svc.EndShiftAsync(_shiftId, why.ToString(), CancellationToken.None);
                yield return Await(t);
                e = t.IsCompleted && !t.IsFaulted && !t.IsCanceled ? t.Result : new ShiftEnd { Error = ShiftError.Network };
                if (e.Error != ShiftError.Network) break;
                yield return new WaitForSeconds(1f + i);
            }
            ui.ShowBusy(null);
            JobSlotRegistry.Shared.Book.Release(_bay.Slot.slotId, why);   // the bot walks back
            ui.ShowShiftChip(false, 0f, 0, 0, null);

            if (e.Ok)
            {
                WalletCache.AdoptServerBalance(e.BalanceAfter, _svc.IsTestStub);
                if (e.Voided)
                    ui.ShowSummary("No pay this time", _trips + " trips. A shift pays after 10 minutes and 3 trips. This one did not count toward your 3 shifts today.", null);
                else
                    ui.ShowSummary("Shift paid", _trips + " trips\nPaid ₦" + e.Paid.ToString("N0") + "\nWallet ₦" + e.BalanceAfter.ToString("N0") + (_svc.IsTestStub ? "  (TEST)" : ""), null);
            }
            else
            {
                string text = ShiftError.PlayerText(e.Error) ?? "Shift closed.";
                ui.ShowSummary("Shift closed", text, null);
            }
            _bay = null;
            _shiftId = null;
            Busy = false;
        }

        // ---- riding at the door ----
        PlayerController _pc;
        CharacterController _cc;

        IEnumerator Board()
        {
            _pc = GetComponent<PlayerController>();
            _cc = GetComponent<CharacterController>();
            Transform seat = _bay.doorSeat ? _bay.doorSeat : _bay.bus;
            // Walk-in: ease to the door seat over 0.4 s (stepping into the bus, not a teleport).
            if (_pc) _pc.enabled = false;
            if (_cc) _cc.enabled = false;
            Vector3 from = transform.position;
            for (float t = 0f; t < StepDownSeconds; t += Time.deltaTime)
            {
                transform.position = Vector3.Lerp(from, seat.position, t / StepDownSeconds);
                yield return null;
            }
            transform.SetParent(seat, true);
            transform.localPosition = Vector3.zero;
        }

        IEnumerator StepDown()
        {
            Transform step = _bay.doorStep ? _bay.doorStep : _bay.Slot.workPoint;
            transform.SetParent(null, true);
            Vector3 from = transform.position, to = step ? step.position : from;
            for (float t = 0f; t < StepDownSeconds; t += Time.deltaTime)
            {
                transform.position = Vector3.Lerp(from, to, t / StepDownSeconds);
                yield return null;
            }
            transform.position = to;
            if (_cc) _cc.enabled = true;
            if (_pc) _pc.enabled = true;
        }

        int Level() => _svc is LocalShiftService local ? local.Ledger.Level : 1;   // display only; the server prices trips

        static IEnumerator Await(Task t) { while (!t.IsCompleted) yield return null; }
    }
}
