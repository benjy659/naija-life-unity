# M4: Jobs loop (job slots, bot handover, danfo conductor shift)

**Status:** SPEC, ready to build. **Author:** Claude (lead engineer), 2026-11-05.
**Builds:** Ted's team, tasks M4.1 to M4.5 in TASKS.md.
**Sources:** docs/DESIGN-INVENTORY.md section 2 (Pawa), 6 (jobs), 7 (economy);
docs/MIGRATION-PLAN.md section 6 (M4); browser design (reference only, no code
carries over): benjy659/naija-life docs/jobs-world-spec.md (JW-1 to JW-7),
docs/economy-balance-sheet.md sections 0, 1, 6, js/nl-workjobs.js conductor
numbers.
**Pacing check:** `node tools/jobsim.js` (same numbers as sections 6 and 9).
**Nothing here is DONE until** Ted's team posts Editor screenshots or a clip
and a WebGL build check under `qa/m4.<n>/` (section 11). Until then every item
is UNVERIFIED.

---

## 1. The owner rule this milestone builds

Benjamin, 2026-10-16: "We have to build this game dynamic. It has its own
play, but real players can do jobs if they are available."

The city works with no player in it. Bots do every job you can see. A real
player can take any job a bot is doing when that job is free; the bot steps
aside and takes the job back when the player lets go. M4 builds the one
mechanism every later job uses (the job-slot system) and one job end to end
(danfo conductor at Oshodi).

Money stays hard to earn (owner, standing): "making money shouldn't be very
easy, people would have to work for it to earn it. No cheating." The client
never creates money. Pay is decided and written by the server.

## 2. Scope

**In M4:**
- Job-slot system: slot registry, slot holders, claim and release, one job at a
  time, release triggers.
- Bot worker handover: walk up, one prompt, bot walks aside and waits, walks
  back when you let go. Never vanishes, never teleports.
- Danfo conductor shift at the Oshodi danfo park: clock in, load the bus
  (shout), ride the route and collect fares (change), drop off, ride back, get
  paid at clock out.
- Pawa need (stamina for work): gate of 25 to clock in, drain while working.
- Shift service: one C# interface with two implementations, an Editor test
  stub and the Supabase client. Server side: three RPCs and two tables
  (section 9, drafted here, written and applied by Ted's team).
- 3 paid shifts per WAT day.

**Not in M4 (later milestones, do not build now):** other jobs (okada, ride
driver, POS, mechanic, tailor, hawker, trader: they reuse this system after
M4), formal careers with interviews, reputation, multiplayer slot claims (the
interface is here, the network half is M5), food and sleep to refill Pawa
(M6 props; a dev menu covers testing), B-38 transfers (owner-gated, untouched).

## 3. Code layout

All under `Assets/Scripts/`. Namespaces `NL.Jobs`, `NL.Economy`, `NL.Player`.
Gameplay code never calls Supabase or Fusion directly; it goes through the
interfaces below (same rule as the netcode abstraction in TASKS.md M5.2).

| File | What it is |
|---|---|
| `Jobs/JobKind.cs` | `enum JobKind { Conductor }` (more kinds added later, never renamed). |
| `Jobs/JobDefinition.cs` | ScriptableObject per job: kind, display name, prompt text, take radius, leave radius, idle timeout, Pawa drain per minute. Asset: `Assets/Settings/Jobs/Conductor.asset`. |
| `Jobs/JobSlot.cs` | MonoBehaviour on each bot worker's work spot. Holds `SlotId` (stable string, e.g. `oshodi-bay-1-conductor`), `JobDefinition`, `WorkPoint` and `AsidePoints` (two, section 6.4), the `BotWorker`, and the current `Holder`. |
| `Jobs/JobSlotRegistry.cs` | ScriptableObject runtime set (`Assets/Settings/Jobs/JobSlotRegistry.asset`). The only way to claim or release. API in section 4.2. |
| `Jobs/BotWorker.cs` | The bot's state machine and NavMeshAgent moves (section 5). |
| `Jobs/PlayerJobs.cs` | On the player: what the player holds, release triggers, the single job prompt. |
| `Jobs/IJobSlotSync.cs` | Network seam for other players' claims. M4 ships `LocalJobSlotSync` (always confirms). M5 adds the Fusion one. |
| `Jobs/Conductor/ConductorShift.cs` | The conductor loop (section 6). |
| `Jobs/Conductor/ShoutMinigame.cs` | Loading minigame. |
| `Jobs/Conductor/ChangeMinigame.cs` | Fare and change minigame. |
| `Jobs/Conductor/DanfoRoute.cs` | Waypoint route the bay's danfo follows (section 6.4). |
| `Jobs/Conductor/PassengerQueue.cs` | Waiting passengers at the bay (greybox capsules until character art). |
| `Player/PlayerNeeds.cs` | Pawa (0 to 100). Hunger and energy come later. |
| `Economy/IShiftService.cs` | Start, record trip, end. Section 8. |
| `Economy/LocalShiftStub.cs` | Editor and development builds only. Same rules as the server. Section 8.3. |
| `Economy/SupabaseShiftService.cs` | Calls the three RPCs with the player's session token. |
| `Economy/WalletCache.cs` | Read cache of the server balance. `AdoptServerBalance(long)` is the only setter. |

## 4. Job-slot system

### 4.1 Rules (carried over from the browser design, JW-1 to JW-7)

- **JS-1. Every job has bots doing it.** A job with no bot worker in the world
  is not built. Oshodi in M4 has 3 conductor slots, one per danfo bay.
- **JS-2. A job is a slot.** One bot worker holds one slot. Holder is one of
  `Bot`, `Local` (this player), `Remote` (another real player, M5).
- **JS-3. Taking over is visible and gentle.** Section 5.
- **JS-4. One job at a time.** `PlayerJobs` holds at most one slot. Claiming a
  second one is refused (`busy`).
- **JS-5. The work is real.** Holding a slot starts the job's work loop. No
  work, no pay.
- **JS-6. Remote holders.** A slot held by another real player shows as taken
  and cannot be claimed. Until M5, `LocalJobSlotSync` confirms every claim.
- **JS-7. One mechanism.** Every future job uses `JobSlotRegistry`. Never
  build a second handover.

### 4.2 Registry API

```csharp
public enum SlotHolder { Bot, Local, Remote }

public sealed class JobSlotRegistry : ScriptableObject {
  public event Action<JobSlot, SlotHolder> SlotChanged;
  public void Register(JobSlot slot);            // called by JobSlot.OnEnable
  public void Unregister(JobSlot slot);          // JobSlot.OnDisable
  public JobSlot Held { get; }                   // the local player's slot or null
  public bool Available(JobSlot slot);           // Holder == Bot && Held == null
  public JobSlot NearestFree(JobKind kind, Vector3 pos, float maxDist);
  public void TryClaim(JobSlot slot, Action<ClaimResult> done); // async: waits on IJobSlotSync
  public void Release(JobSlot slot, ReleaseReason why);
}
public enum ClaimResult { Ok, Busy, Taken, TooFar, NoPawa, ShiftCap, Offline }
public enum ReleaseReason { WalkedAway, Idle, ShiftEnded, PawaEmpty, OtherAction, Disconnected }
```

`TryClaim` checks in this order: player holds nothing (`Busy`), slot holder is
`Bot` (`Taken`), player within the job's take radius (`TooFar`), then the job's
own gate (conductor: Pawa and the server's shift start, section 7). Only after
all pass does the holder become `Local` and the bot step aside. A refused
claim changes nothing in the world.

### 4.3 Release triggers (conductor values, in `Conductor.asset`)

| Trigger | Value | What the player sees |
|---|---|---|
| Walked away from the bay (not on the bus) | more than 8 m | "You left the bus. The conductor takes it back." |
| Idle at the bay (no shout, no tap) | 180 s | "The conductor takes his bus back." |
| Shift time up | 15 min after clock in (section 7) | Shift summary card, then release |
| Pawa reaches 0 | | "You don tire. Shift over." then release |
| Player taps End shift | | Release at the bay. During a ride: "We go finish this trip first." and release on arrival |
| Player starts another action that needs hands (enter another vehicle, another job prompt) | | Same as End shift |
| App closed or connection lost | | Server closes the shift lazily (section 9) |

### 4.4 Prompt

One prompt at a time for the whole game (owner rule). `PlayerJobs` asks the
shared prompt service for a single action prompt when the player is within
2.6 m of a free slot: **"Take over from this conductor"**, sub line
"₦600 and up a trip" (honest: a full bus at level 1 pays about ₦680).
The prompt never sits over the player or the joystick; placement belongs to
the UI/HUD workstream's prompt service. Free slots also show a soft ground
ring under the bot (no floating text).

## 5. Bot handover (BotWorker)

States: `Working` -> `SteppingAside` -> `WaitingAside` -> `WalkingBack` ->
`Working`.

- **Working.** Bot stands at `WorkPoint`, plays shout barks every 6 to 10 s
  (routes: "Yaba! Yaba!", "Ojuelegba straight!", "CMS! CMS!", "Ikeja along!",
  "Obalende! Obalende!", "Enter with your change!"), and does the same trips a
  player would (section 6), with no money involved. The world looks busy with
  no player present.
- **SteppingAside.** On claim: one line ("Oya, try am."), then walk with the
  NavMeshAgent at 1.3 m/s with the real walk animation to the aside point
  farther from the player (section 6.4): 2 to 3 m from the bus door, on the
  island, never on the road and never between the player and the door.
- **WaitingAside.** Faces the bus, idle variants, a bark every 40 to 90 s
  ("Shout am well!", "No dash dem change o."). Never boards the bus.
- **WalkingBack.** On release: wait 1 s, walk back to `WorkPoint` at 1.3 m/s,
  then resume `Working`. If the bus is out on a trip, the bot waits at the
  work point for it.
- **Never teleport.** If a path fails, the bot stays where it is and retries
  every 2 s. A bot never snaps to a point while any camera can see it.

## 6. The danfo conductor shift

### 6.1 Flow (one trip, about 3 minutes)

1. **Clock in.** Player taps the prompt. Client checks Pawa >= 25, then calls
   `StartShift` (section 8). On `ok` the claim completes, the bot steps aside,
   the shift chip appears (time left, trips, earned so far). On an error the
   prompt shows why (section 8.2) and nothing changes.
2. **Load (shout), up to 45 s.** The bay's danfo is parked with its doors open.
   The action button (bottom right) becomes the Shout button with a ring that
   shrinks from 2x to 0.4x each cycle. A tap while the ring is on the gold band
   calls one waiting passenger, who walks to the door and boards (walks in, is
   not removed in view). An early tap locks the button for 450 ms ("Too early!
   Passengers dey look you."). The seat strip shows 14 seats.
   Gold band half-width `0.16 + 0.012 x level`; ring period
   `1.35 - 0.015 x level` seconds.
3. **Depart.** When 14 seats fill or 45 s pass. With fewer than 4 passengers
   the bot driver leaves without you ("I no fit wait again."), no trip counts,
   30 s before the next bus is ready.
4. **Ride out, about 70 s.** Player hangs at the sliding door (conductor pose,
   seat-locked, joystick hidden). Camera keeps the steady follow rule on the
   bus. The bus follows `DanfoRoute` to the drop stop.
5. **Collect fares (change), during the ride.** 4 passengers pay. Fare is ₦500.
   Each hands a ₦1,000 or ₦2,000 note; the change sheet (bottom, at most 35%
   of the screen height, never over the player) shows three amounts, one
   right. 6 s to answer; no answer counts as wrong. Right: "Correct." Wrong:
   "My change no complete!" or "You don dash am ₦X!".
6. **Drop, about 15 s.** Passengers walk off at the stop.
7. **Ride back, about 50 s,** empty, to the bay.
8. **Trip recorded.** On arrival the client calls `RecordTrip(seats, right, 4)`.
   The server checks and prices it; the chip shows the server's trip pay as
   "earned this shift" (not yet in the wallet).
9. Repeat from 2 until the player ends the shift or a release trigger fires.
10. **Clock out.** Client calls `EndShift`. The summary card shows trips, the
    server's pay and the new balance from `balance_after`. The bot walks back.

### 6.2 Pay (decided by the server; the client only shows it)

```
level      = min(10, 1 + floor(sqrt(lifetime_trips / 25)))     -- server count
mult       = 0.65 + 0.085 x (level - 1)                        -- 0.65 .. 1.415
trip_base  = 60 x seats + (seats = 14 ? 200 : 0) - 100 x (fares - right)
trip_pay   = max(0, round_to_10(trip_base x mult))             -- 0 if seats < 4
shift_pay  = min(sum(trip_pay), 6 x trip_pay(14 seats, 4 right, level))
```

`tools/jobsim.js` output (2026-11-05):

| | Level 1 | Level 3 | Level 5 | Level 10 |
|---|---|---|---|---|
| Perfect trip | ₦680 | ₦850 | ₦1,030 | ₦1,470 |
| Shift ceiling (6 perfect trips) | ₦4,080 | ₦5,100 | ₦6,180 | ₦8,820 |

A steady player (12 seats, 3 of 4 fares right, 5 trips a shift, 3 shifts a day,
45 minutes of play a day): day 1 ₦6,000, first month about ₦237,000, level 10
after about 6 months, about ₦4.4M in a year. This matches the balance sheet's
rung 1 (₦2,800 to ₦4,000 a shift, about ₦211,000 a month) and real Lagos
conductor wages. Conductor work alone would take about 2,470 days to reach the
₦30M mansion deposit: jobs are the first rung, and the mansion stays a long
goal reached through business and property (owner pacing decision,
2026-10-17). A cheater who fakes perfect trips still cannot beat ₦12,240 a day
at level 1, because the server caps trips by time and count.

### 6.3 Shift limits

- Shift length: up to 15 minutes of trips from clock in. A trip that arrives
  after 15 min still counts if it departed before.
- Minimum to be paid: 10 minutes from clock in to clock out AND at least 3
  trips. Less is a void shift: no pay, and it does not use one of the 3 daily
  shifts. The chip says this up front ("Work 10 minutes to get paid").
- At most 6 trips per shift; at least 150 s between trips (server clock).
- 3 paid shifts per WAT day (Africa/Lagos midnight resets).
- After the 3rd shift the prompt reads "You don work 3 shifts today. Come back
  tomorrow." and the claim is refused before the bot moves.

### 6.4 The bay, the bus and the route (needs from the M2 Oshodi greybox)

- 3 danfo bays in the Oshodi park, each with a parked yellow danfo (14 seats,
  sliding door on the kerb side), a `JobSlot` with `WorkPoint`, two
  `AsidePoints` and a `PassengerQueue` of up to 6 waiting passengers. Phone
  budget: at most 12 skinned NPCs on screen, so queues refill as passengers
  board.
- `DanfoRoute`: hand-placed waypoints, about 600 m out to one drop stop and
  back, right-hand lane, no props in the lane. The bus is a kinematic follower
  (8 to 10 m/s, eases in and out of stops, no traffic AI in M4). The bus is the
  M3 danfo model and prefab, driven by the route, not by WheelColliders.
- **Later, with M3 traffic:** M3 now has `LaneGraph` and `TrafficDriver`
  (Assets/Scripts/Vehicles). Once M3 is verified, the bay danfo should drive
  its route with `TrafficDriver` on the lane graph so it reacts to traffic,
  and `DanfoRoute` keeps only the stop and bay events. Not in M4.
- **Placed in the M2 layout (Oshodi thread, 2026-11-05):**
  `docs/specs/oshodi-greybox-layout.json` (checked by
  `tools/oshodi/oshodi-layout.js`), spec `docs/specs/oshodi-greybox.md`, branch
  `claude/project-thread-ffwnry`. `danfoPark.bays[0..2]` carry `m4: true`
  (bay-1 UNILAG GATE, bay-2 IKEJA ALONG, bay-3 MILE 2), park west of Agege
  Motor Road by Terminal 3, buses face east with the sliding door on a raised
  2 m island. Each bay has a `workPoint` at the door and TWO aside points
  (`aside-west`, `aside-east`, 2.3 to 2.5 m from the door along the island).
  `BotWorker` picks the aside point farther from where the player stepped onto
  the island, so the bot is never between the player and the door. Queue: 6
  spots per bay, 0.8 m apart, behind `aside-west`. Drop stop: lay-by on the
  southbound side of the south approach road (scene `Approach_South`), 597.5 m
  from the park exit, with `stopPoint` and `alightPoint`; turning point is the
  roundabout at z -720. The route is an out-and-back. Any new scene need for
  M4 goes to the Oshodi thread so its layout checker covers it.

## 7. Pawa

- `PlayerNeeds.Pawa`, 0 to 100, starts at 100 for a new player.
- Clock in needs Pawa >= 25. Below it: "You too tire. Chop first. (Pawa 20/25)".
- Drains 1.2 per minute while holding a conductor slot (about 18 a shift, so
  3 shifts use about 54). At 0 the shift ends (paid if section 6.3 is met).
- Refilling Pawa (food, sleep) arrives with M6 props. For M4 testing, a dev
  menu (development builds only) sets Pawa.
- Known gap, written down on purpose: in M4 the client reports Pawa at clock
  in and the server only checks it is 25..100. Server-side needs come later;
  the server's time and trip limits are what keep pay honest meanwhile.

## 8. Shift service (client side)

### 8.1 Interface

```csharp
public interface IShiftService {
  void StartShift(JobKind job, string slotId, int pawa, Action<ShiftStart> done);
  void RecordTrip(string shiftId, int seats, int faresRight, int faresTotal, Action<TripResult> done);
  void EndShift(string shiftId, string reason, Action<ShiftEnd> done);
}
// ShiftStart { bool ok; string error; string shiftId; int shiftsToday; DateTime endsBy; }
// TripResult { bool ok; string error; int tripNo; long tripPay; long shiftPending; }
// ShiftEnd   { bool ok; string error; long paid; bool voided; long balanceAfter; }
```

Every call carries a client reference (new GUID per user action, reused on
retry) so a retry never pays twice.

### 8.2 Errors and what the player sees

| Code | When | Text |
|---|---|---|
| `pawa_low` | Pawa < 25 | "You too tire. Chop first." |
| `cap_reached` | 3 paid shifts today | "You don work 3 shifts today. Come back tomorrow." |
| `busy` | an open shift already exists | "Finish your other shift first." |
| `too_fast` | trip less than 150 s after the last | trip not counted, no text (log only) |
| `too_many` | 7th trip in a shift | "Shift full. Clock out to get paid." |
| `not_open` | shift closed or not yours | close the chip, release the slot |
| `bad_input` | seats or fares out of range | trip not counted, log only |
| network failure | | "Saving your pay..." with a spinner, retry 3 times with the same reference, then "We go save am when network come back." The server still holds the open shift (section 9 lazy close). |

No call ever leaves the player waiting without a progress state.

### 8.3 LocalShiftStub (Editor and test builds)

Same rules and numbers as section 6 and 9, kept in memory only. The wallet
chip shows "TEST" next to the amount. Nothing is saved, nothing syncs. A
release build must refuse to use it:
`#if !UNITY_EDITOR && !DEVELOPMENT_BUILD` throws on construction. The stub is
how M4 is played before Ted's migration is live and before M5 sign-in exists.

### 8.4 Wallet

`WalletCache` holds the last server balance. It changes only through
`AdoptServerBalance(balance_after)` from a server reply. The client never adds
pay to the wallet itself. The phone wallet in M6 reads this cache.

## 9. Server side (Ted's team writes and applies; Claude reviews the SQL first)

Supabase project `jdumcxvqjtgnbeuvlufq` (shared with the frozen browser game,
so names are new and nothing existing changes). Ted owns the migration file,
its number and applying it. Below is the contract and a draft to start from.

### 9.1 Rules

- New ledger source `job_shift`: server only (`client_allowed = false`), one
  row per paid shift, `max_amt 10000`, `daily_cap 30000`. The client cannot
  post it through the batch RPC.
- The Unity client never posts `job_payout` rows. Job pay comes only from
  `nl_shift_end`. (The browser game's client-allowed `job_payout` stays as it
  is for the frozen site; the Unity client simply never uses it.)
- Tables: RLS on, select own rows only, no insert, update or delete grants to
  `anon` or `authenticated`. Explicit GRANTs (Supabase change of 2026-10-30:
  new tables get no automatic Data API grants). EXECUTE on the three RPCs to
  `authenticated` only, revoked from `anon` and `public`.
- Every RPC takes the per-user advisory lock (same pattern as `nl_loan_take`
  in browser migration 0019) and uses the server clock only.
- Lazy close: every RPC first closes the caller's open shift if it started
  more than 20 minutes ago (paid if the section 6.3 minimum is met, else
  void). No cron needed.
- B-38 note for whoever ports transfers: the eligibility rule "at least 3
  shifts or sales ever" must count `job_shift` rows too (browser 0040 counted
  only `job_payout` and `sale`). B-38 stays owner-gated; this is a note only.

### 9.2 Draft (starting point, not final)

```sql
create table if not exists public.nl_job_shifts (
  id          uuid primary key default gen_random_uuid(),
  user_id     uuid not null references auth.users(id) on delete cascade,
  job         text not null check (job in ('conductor')),
  slot_id     text not null check (length(slot_id) between 1 and 64),
  started_at  timestamptz not null default now(),
  ended_at    timestamptz,
  status      text not null default 'open' check (status in ('open','paid','void')),
  pawa_in     int not null check (pawa_in between 25 and 100),
  trips       int not null default 0,
  pending     bigint not null default 0,
  paid        bigint not null default 0,
  wat_day     date not null default (now() at time zone 'Africa/Lagos')::date,
  client_ref  text not null,
  unique (user_id, client_ref)
);
create unique index if not exists nl_job_shifts_one_open
  on public.nl_job_shifts (user_id) where status = 'open';

create table if not exists public.nl_job_trips (
  shift_id     uuid not null references public.nl_job_shifts(id) on delete cascade,
  trip_no      int not null,
  at           timestamptz not null default now(),
  seats        int not null check (seats between 0 and 14),
  fares_right  int not null check (fares_right between 0 and 4),
  fares_total  int not null check (fares_total = 4),
  pay          bigint not null,
  client_ref   text not null,
  primary key (shift_id, trip_no),
  unique (shift_id, client_ref)
);

alter table public.nl_job_shifts enable row level security;
alter table public.nl_job_trips  enable row level security;
create policy nl_job_shifts_own on public.nl_job_shifts for select using (user_id = auth.uid());
create policy nl_job_trips_own on public.nl_job_trips for select
  using (exists (select 1 from public.nl_job_shifts s where s.id = shift_id and s.user_id = auth.uid()));
revoke all on public.nl_job_shifts, public.nl_job_trips from anon, authenticated;
grant select on public.nl_job_shifts, public.nl_job_trips to authenticated;

insert into public.nl_ledger_source_rules (source, min_amt, max_amt, daily_cap, client_allowed, note)
values ('job_shift', 1, 10000, 30000, false, 'job shift pay, written only by nl_shift_end')
on conflict (source) do update set max_amt = excluded.max_amt,
  daily_cap = excluded.daily_cap, client_allowed = false, note = excluded.note;

-- Helpers (security definer, not granted to clients):
--   nl_job_level(uid, job) -> int    : 1 + floor(sqrt(counted trips / 25)), max 10,
--                                       counted = trips with seats >= 4 in paid shifts
--   nl_trip_pay(seats, right, level) -> bigint : section 6.2 formula
--   nl_shift_close(shift_id)          : pays or voids; writes ONE ledger row
--       (user, 'shift:' || id, now(), paid, prev balance + paid, 'Conductor shift', 'job_shift')
--       paid = least(pending, 6 * nl_trip_pay(14, 4, level)); void if
--       now() - started_at < 10 min or trips < 3.

-- nl_shift_start(p_job text, p_slot text, p_pawa int, p_ref text) returns jsonb
--   lock; lazy close; errors: pawa_low (p_pawa < 25), busy (open shift),
--   cap_reached (3 'paid' shifts with wat_day = today WAT);
--   insert shift; return {ok, shift_id, shifts_today, ends_by = started_at + 15 min}.
--   Same p_ref twice returns the same shift (idempotent).
-- nl_shift_trip(p_shift uuid, p_seats int, p_right int, p_total int, p_ref text) returns jsonb
--   lock; lazy close; not_open unless open and owned; bad_input on range;
--   too_many if trips = 6; too_fast if now() - (last trip at, or started_at) < 150 s;
--   no trip after started_at + 20 min (a trip started by 15 min arrives by about 19); pay = nl_trip_pay(...) with the SERVER level;
--   return {ok, trip_no, trip_pay, shift_pending}. Same p_ref = same answer.
-- nl_shift_end(p_shift uuid, p_reason text) returns jsonb
--   lock; nl_shift_close; return {ok, paid, voided, balance_after}.
--   Calling it on an already closed shift returns the same result (idempotent).
```

### 9.3 What the server can and cannot prove in M4

Seats and right answers come from the client's minigame, so a cheater can
claim perfect trips. The server bounds the damage: trip spacing, 6 trips a
shift, 3 shifts a day and the per-shift ceiling hold it to the honest
maximum (₦12,240 a day at level 1). Flag for review (row in `economy_flags`
later): perfect trips on every trip for 20 shifts in a row. Fusion Server Mode
and server-side needs come after M6.

## 10. Bot trips with no player

A bot conductor runs the same loop on a timer: loads for 30 to 45 s with
NPC passengers, rides the route at the door, comes back. No RPC calls, no
money. With no player present the three bays should show buses leaving and
arriving at staggered times so the park always looks alive.

## 11. Acceptance and QA evidence (Ted's team posts, Claude verifies)

Post under `qa/m4.<n>/` with a short `REPORT.md`. Editor play AND WebGL
build, at 390x844 and 1280x800. A clip is better than screenshots for
movement.

1. **Busy park, no player:** all three bays working, at least one bus out on
   the route (clip, 30 s).
2. **Take over:** walk up, one prompt, tap, bot walks aside on the pavement
   (clip shows no vanish and no snap).
3. **One full trip:** shout fills seats, ride out with the door view, change
   sheet answered, drop, ride back, chip shows the trip pay.
4. **Clock out paid:** summary card with server pay and new balance (stub
   says TEST).
5. **Short shift void:** clock out after 2 trips, card says no pay and why.
6. **Gates:** Pawa 20 refused with the text; 4th shift of the day refused.
7. **Release:** walk 9 m away, bot walks back and resumes shouting; same after
   180 s idle.
8. **Nothing blocks the screen** at 390x844 during shout and change: player
   visible, joystick area clear.
9. **Server (after the migration is applied):** a SQL test script that
   calls the RPCs as a test user and shows: too_fast, too_many, cap_reached,
   void under 10 min, idempotent retry, and exactly one `job_shift` ledger row
   per paid shift.

UX check for every item: easy on phone and laptop, nothing blocking the
screen, possible in real Lagos, no notification spam.
