-- ============================================================================
-- Naija Streets (Unity) - M4.4: job shifts paid by the server (DRAFT, 2026-11-06)
-- Migration 0042: nl_job_shifts, nl_job_trips, nl_shift_start/trip/end,
-- ledger source job_shift. Spec: docs/specs/jobs-m4.md sections 6 and 9.
--
-- STATUS: DRAFT by Claude (jobs thread), NOT applied. Ted's team owns
-- migrations: review, adjust, apply. Claude never applies. Post the run of
-- the test block at the bottom under qa/m4.4/.
--
-- WHAT IT DOES: the client asks to start a shift, reports each trip (seats
-- and right answers from the minigames), and asks to end. The SERVER checks
-- every limit on its own clock, prices every trip with its own skill level,
-- and writes ONE ledger row per paid shift (source job_shift, server only).
-- The client never sends an amount.
--
-- SHAPE: two new tables, three new client RPCs, three private helpers, one
-- new row in nl_ledger_source_rules. No ALTER on any live object. The same
-- numbers live in Assets/Scripts/Jobs/Core/ShiftRules.cs and tools/jobsim.js;
-- change all three together.
--
-- Replies are jsonb {ok:false, error:<code>} for player-facing refusals (the
-- client shows a line for each); exceptions only for "sign-in required".
-- Codes: pawa_low, cap_reached, busy, too_fast, too_many, not_open, bad_input.
--
-- GRANTS (Supabase change of 2026-10-30): explicit. Tables: select own rows
-- only, nothing else for anon/authenticated. RPCs: EXECUTE to authenticated.
-- ============================================================================

-- ------------------------------------------------------------------ tables --
create table if not exists public.nl_job_shifts (
  id          uuid primary key default gen_random_uuid(),
  user_id     uuid not null references auth.users(id) on delete cascade,
  job         text not null check (job in ('conductor')),
  slot_id     text not null check (length(slot_id) between 1 and 64),
  started_at  timestamptz not null default now(),
  last_trip_at timestamptz not null default now(),
  ended_at    timestamptz,
  status      text not null default 'open' check (status in ('open','paid','void')),
  pawa_in     int not null check (pawa_in between 25 and 100),
  trips       int not null default 0 check (trips between 0 and 6),
  pending     bigint not null default 0,
  paid        bigint not null default 0,
  end_reason  text,
  wat_day     date not null default ((now() at time zone 'Africa/Lagos')::date),
  client_ref  text not null check (length(client_ref) between 1 and 64),
  unique (user_id, client_ref)
);
create unique index if not exists nl_job_shifts_one_open
  on public.nl_job_shifts (user_id) where status = 'open';
create index if not exists nl_job_shifts_user_day
  on public.nl_job_shifts (user_id, wat_day) where status = 'paid';

create table if not exists public.nl_job_trips (
  shift_id     uuid not null references public.nl_job_shifts(id) on delete cascade,
  trip_no      int not null check (trip_no between 1 and 6),
  at           timestamptz not null default now(),
  seats        int not null check (seats between 0 and 14),
  fares_right  int not null check (fares_right between 0 and 4),
  fares_total  int not null check (fares_total = 4),
  level        int not null check (level between 1 and 10),
  pay          bigint not null check (pay >= 0),
  client_ref   text not null check (length(client_ref) between 1 and 64),
  primary key (shift_id, trip_no),
  unique (shift_id, client_ref)
);

alter table public.nl_job_shifts enable row level security;
alter table public.nl_job_trips  enable row level security;

drop policy if exists nl_job_shifts_own on public.nl_job_shifts;
create policy nl_job_shifts_own on public.nl_job_shifts
  for select to authenticated using (user_id = auth.uid());
drop policy if exists nl_job_trips_own on public.nl_job_trips;
create policy nl_job_trips_own on public.nl_job_trips
  for select to authenticated using (exists (
    select 1 from public.nl_job_shifts s where s.id = shift_id and s.user_id = auth.uid()));

revoke all on public.nl_job_shifts, public.nl_job_trips from public, anon, authenticated;
grant select on public.nl_job_shifts, public.nl_job_trips to authenticated;

-- --------------------------------------------------------- ledger source --
insert into public.nl_ledger_source_rules (source, min_amt, max_amt, daily_cap, client_allowed, note)
values ('job_shift', 1, 10000, 30000, false, 'job shift pay, written only by nl_shift_end / nl_shift_close (0042)')
on conflict (source) do update set
  min_amt = excluded.min_amt, max_amt = excluded.max_amt, daily_cap = excluded.daily_cap,
  client_allowed = false, note = excluded.note;

-- ---------------------------------------------------------------- helpers --
-- Skill level: 1 + floor(sqrt(counted trips / 25)), max 10. Counted = trips
-- with 4+ seats in PAID shifts (ShiftRules.Level).
create or replace function public.nl_job_level(p_uid uuid, p_job text)
returns int language sql stable security definer set search_path = public as $$
  select least(10, 1 + floor(sqrt(coalesce((
    select count(*) from public.nl_job_trips t
      join public.nl_job_shifts s on s.id = t.shift_id
     where s.user_id = p_uid and s.job = p_job and s.status = 'paid' and t.seats >= 4
  ), 0) / 25.0))::int);
$$;

-- Trip pay (ShiftRules.TripPay): 0 under 4 seats; (60*seats + 200 if full
-- - 100*wrong) * (0.65 + 0.085*(level-1)), floor 0, rounded to 10 naira.
create or replace function public.nl_trip_pay(p_seats int, p_right int, p_level int)
returns bigint language sql immutable set search_path = public as $$
  select case when p_seats < 4 then 0::bigint else
    (round(greatest(0, 60 * least(p_seats, 14) + case when p_seats >= 14 then 200 else 0 end
                       - 100 * (4 - greatest(0, least(4, p_right))))
           * (0.65 + 0.085 * (greatest(1, least(10, p_level)) - 1)) / 10.0) * 10)::bigint
  end;
$$;

-- Close one open shift: pay (one ledger row) or void. Caller holds the user lock.
create or replace function public.nl_shift_close(p_shift uuid, p_end timestamptz, p_reason text)
returns jsonb language plpgsql security definer set search_path = public as $$
declare
  v_s     public.nl_job_shifts%rowtype;
  v_level int;
  v_paid  bigint;
  v_bal   bigint := 0;
begin
  select * into v_s from public.nl_job_shifts where id = p_shift for update;
  if not found then return jsonb_build_object('ok', false, 'error', 'not_open'); end if;

  select balance_after into v_bal from public.economy_ledger
    where user_id = v_s.user_id order by seq desc limit 1;
  v_bal := coalesce(v_bal, 0);

  if v_s.status <> 'open' then   -- already closed: same answer again (idempotent)
    return jsonb_build_object('ok', true, 'paid', v_s.paid, 'voided', v_s.status = 'void', 'balance_after', v_bal);
  end if;

  if p_end - v_s.started_at < interval '10 minutes' or v_s.trips < 3 then
    update public.nl_job_shifts set status = 'void', ended_at = p_end, end_reason = left(p_reason, 32) where id = p_shift;
    return jsonb_build_object('ok', true, 'paid', 0, 'voided', true, 'balance_after', v_bal);
  end if;

  v_level := public.nl_job_level(v_s.user_id, v_s.job);
  v_paid  := least(v_s.pending, 6 * public.nl_trip_pay(14, 4, v_level));
  update public.nl_job_shifts
     set status = 'paid', ended_at = p_end, paid = v_paid, end_reason = left(p_reason, 32)
   where id = p_shift;
  if v_paid > 0 then
    insert into public.economy_ledger (user_id, client_ref, ts, delta, balance_after, reason, source)
      values (v_s.user_id, 'shift:' || v_s.id::text, now(), v_paid, v_bal + v_paid,
              'Conductor shift, ' || v_s.trips || ' trips', 'job_shift');
    v_bal := v_bal + v_paid;
  end if;
  return jsonb_build_object('ok', true, 'paid', v_paid, 'voided', false, 'balance_after', v_bal);
end $$;

-- Close the caller's open shift if it is older than 20 minutes, as of its last trip.
create or replace function public.nl_shift_lazy_close(p_uid uuid)
returns void language plpgsql security definer set search_path = public as $$
declare v_s public.nl_job_shifts%rowtype;
begin
  select * into v_s from public.nl_job_shifts where user_id = p_uid and status = 'open';
  if found and now() - v_s.started_at > interval '20 minutes' then
    perform public.nl_shift_close(v_s.id,
      case when v_s.last_trip_at > v_s.started_at then v_s.last_trip_at else now() end, 'stale');
  end if;
end $$;

-- ------------------------------------------------------------ client RPCs --
create or replace function public.nl_shift_start(p_job text, p_slot text, p_pawa int, p_ref text)
returns jsonb language plpgsql security definer set search_path = public as $$
declare
  v_uid   uuid := auth.uid();
  v_today date := (now() at time zone 'Africa/Lagos')::date;
  v_s     public.nl_job_shifts%rowtype;
  v_done  int;
begin
  if v_uid is null then raise exception 'sign-in required'; end if;
  perform pg_advisory_xact_lock(hashtext(v_uid::text));
  perform public.nl_shift_lazy_close(v_uid);

  if p_job is distinct from 'conductor' or p_slot is null or length(p_slot) not between 1 and 64
     or p_ref is null or length(p_ref) not between 1 and 64 then
    return jsonb_build_object('ok', false, 'error', 'bad_input');
  end if;

  select count(*) into v_done from public.nl_job_shifts
   where user_id = v_uid and status = 'paid' and wat_day = v_today;

  select * into v_s from public.nl_job_shifts where user_id = v_uid and client_ref = p_ref;
  if found then   -- retry of the same tap
    return jsonb_build_object('ok', true, 'shift_id', v_s.id, 'shifts_today', v_done,
                              'ends_by', v_s.started_at + interval '15 minutes');
  end if;

  if p_pawa is null or p_pawa < 25 or p_pawa > 100 then
    return jsonb_build_object('ok', false, 'error', 'pawa_low');
  end if;
  if exists (select 1 from public.nl_job_shifts where user_id = v_uid and status = 'open') then
    return jsonb_build_object('ok', false, 'error', 'busy');
  end if;
  if v_done >= 3 then
    return jsonb_build_object('ok', false, 'error', 'cap_reached');
  end if;

  insert into public.nl_job_shifts (user_id, job, slot_id, pawa_in, client_ref, wat_day)
    values (v_uid, p_job, p_slot, p_pawa, p_ref, v_today)
    returning * into v_s;
  return jsonb_build_object('ok', true, 'shift_id', v_s.id, 'shifts_today', v_done,
                            'ends_by', v_s.started_at + interval '15 minutes');
end $$;

create or replace function public.nl_shift_trip(p_shift uuid, p_seats int, p_right int, p_total int, p_ref text)
returns jsonb language plpgsql security definer set search_path = public as $$
declare
  v_uid   uuid := auth.uid();
  v_s     public.nl_job_shifts%rowtype;
  v_t     public.nl_job_trips%rowtype;
  v_level int;
  v_pay   bigint;
begin
  if v_uid is null then raise exception 'sign-in required'; end if;
  perform pg_advisory_xact_lock(hashtext(v_uid::text));
  perform public.nl_shift_lazy_close(v_uid);

  select * into v_s from public.nl_job_shifts where id = p_shift and user_id = v_uid for update;
  if not found or v_s.status <> 'open' then
    return jsonb_build_object('ok', false, 'error', 'not_open');
  end if;

  if p_ref is not null then   -- retry of the same trip
    select * into v_t from public.nl_job_trips where shift_id = p_shift and client_ref = p_ref;
    if found then
      return jsonb_build_object('ok', true, 'trip_no', v_t.trip_no, 'trip_pay', v_t.pay, 'shift_pending', v_s.pending);
    end if;
  end if;

  if p_ref is null or length(p_ref) not between 1 and 64
     or p_seats is null or p_seats not between 0 and 14
     or p_total is distinct from 4 or p_right is null or p_right not between 0 and 4 then
    return jsonb_build_object('ok', false, 'error', 'bad_input');
  end if;
  if v_s.trips >= 6 then return jsonb_build_object('ok', false, 'error', 'too_many'); end if;
  if now() - v_s.last_trip_at < interval '150 seconds' then
    return jsonb_build_object('ok', false, 'error', 'too_fast');
  end if;
  if now() - v_s.started_at > interval '20 minutes' then
    return jsonb_build_object('ok', false, 'error', 'not_open');
  end if;

  v_level := public.nl_job_level(v_uid, v_s.job);
  v_pay   := public.nl_trip_pay(p_seats, p_right, v_level);
  insert into public.nl_job_trips (shift_id, trip_no, seats, fares_right, fares_total, level, pay, client_ref)
    values (p_shift, v_s.trips + 1, p_seats, p_right, p_total, v_level, v_pay, p_ref);
  update public.nl_job_shifts
     set trips = v_s.trips + 1, pending = v_s.pending + v_pay, last_trip_at = now()
   where id = p_shift;
  return jsonb_build_object('ok', true, 'trip_no', v_s.trips + 1, 'trip_pay', v_pay,
                            'shift_pending', v_s.pending + v_pay);
end $$;

create or replace function public.nl_shift_end(p_shift uuid, p_reason text)
returns jsonb language plpgsql security definer set search_path = public as $$
declare v_uid uuid := auth.uid();
begin
  if v_uid is null then raise exception 'sign-in required'; end if;
  perform pg_advisory_xact_lock(hashtext(v_uid::text));
  perform public.nl_shift_lazy_close(v_uid);
  if not exists (select 1 from public.nl_job_shifts where id = p_shift and user_id = v_uid) then
    return jsonb_build_object('ok', false, 'error', 'not_open');
  end if;
  return public.nl_shift_close(p_shift, now(), coalesce(p_reason, 'end'));
end $$;

-- ----------------------------------------------------------------- grants --
revoke all on function public.nl_job_level(uuid, text) from public, anon, authenticated;
revoke all on function public.nl_trip_pay(int, int, int) from public, anon, authenticated;
revoke all on function public.nl_shift_close(uuid, timestamptz, text) from public, anon, authenticated;
revoke all on function public.nl_shift_lazy_close(uuid) from public, anon, authenticated;
revoke all on function public.nl_shift_start(text, text, int, text) from public, anon;
revoke all on function public.nl_shift_trip(uuid, int, int, int, text) from public, anon;
revoke all on function public.nl_shift_end(uuid, text) from public, anon;
grant execute on function public.nl_shift_start(text, text, int, text) to authenticated;
grant execute on function public.nl_shift_trip(uuid, int, int, int, text) to authenticated;
grant execute on function public.nl_shift_end(uuid, text) to authenticated;

-- ============================================================================
-- CHECKS for Ted after applying (run as a signed-in TEST user in the SQL
-- editor with `set local role authenticated; set local request.jwt.claims =
-- '{"sub":"<test uuid>"}';`, or from the client). Post results to qa/m4.4/.
--   1. select public.nl_trip_pay(14,4,1), nl_trip_pay(14,4,10), nl_trip_pay(12,3,1), nl_trip_pay(3,4,1);
--      expect 680, 1470, 400, 0  (same as Edit Mode test TripPay_Matches_Jobsim)
--   2. nl_shift_start('conductor','oshodi-bay-1-conductor',24,'a') -> pawa_low
--   3. nl_shift_start(... ,80,'b') -> ok; again with 'c' -> busy; again with 'b' -> same shift
--   4. nl_shift_trip(<id>,14,4,4,'t1') at once -> too_fast (150 s rule)
--   5. nl_shift_end(<id>,'x') with 0 trips -> voided true, no ledger row
--   6. after a real 3-trip, 10-minute shift: exactly one economy_ledger row,
--      source job_shift, client_ref 'shift:<id>'; nl_shift_end again -> same answer
--   7. as the client: insert into nl_job_shifts ... -> permission denied;
--      nl_ledger_post_batch with source job_shift -> "server-only source"
-- ============================================================================
