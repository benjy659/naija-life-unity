-- ============================================================================
-- Naija Life Unity — Migration 0002: economy ledger tables
-- (Unity port of browser 0009/0019 table set — same contracts, fresh Unity
--  numbering, standalone + re-runnable)
--
-- WHAT THIS CREATES
--   public.economy_ledger         — append-only transaction log. balance_after
--                                    is SERVER-computed; the client never
--                                    decides a balance. seq (identity) is the
--                                    ordering key, not time.
--   public.economy_flags          — append-only anomaly flags.
--   public.nl_ledger_source_rules — the single place that prices money.
--                                    Amounts are whole naira. client_allowed
--                                    = false means only server code writes it.
--   public.nl_economy_config      — one-time opening-balance cutoff stamp.
--   public.nl_loans               — server-side loan book.
--   public.nl_inventory           — server-side seller inventory.
--   public.nl_item_prices         — server price list (the ONLY prices
--                                    nl_sale_verified trusts).
--   nl_validate_ledger_row()      — defense-in-depth trigger: flags absurd
--                                    rows (never rejects them) on EVERY
--                                    ledger insert, including service_role
--                                    writes the RPC rules do not cover.
--
-- MONEY RULE (migration plan §4): all transfers, purchases, earnings and
-- property changes are validated in Supabase RPCs with RLS. Fusion never
-- decides who owns what or how much money anyone has. The client NEVER
-- writes these tables directly: RLS allows owners to READ their own rows
-- only; INSERT/UPDATE/DELETE are revoked from anon/authenticated. Every
-- write goes through the RPCs in 0003 (security definer, bypass RLS) or
-- service_role (edge functions, bypass RLS).
--
-- SOURCES SEEDED HERE (browser 0019 caps, unchanged): job_payout, sale,
-- purchase, fine, loan, loan_repay, gift, game, opening_balance,
-- pack_credit, starter_grant, family_stipend. The M4 shift source
-- `job_shift` is NOT seeded here — M4.4 owns it (its migration inserts
-- the row). Business/rent sources (business_profit, property_rent) are
-- also NOT seeded here — they arrive with the property/business work.
--
-- GRANTS (Oct 30, 2026 change): explicit grants in the same migration.
-- Ledger/flags/loans/inventory: owners read only. Rules/config/prices:
-- server config, no grants at all.
--
-- STATUS: written 2026-11-05 (Unity backend builder). NOT APPLIED to the
-- live project — live migrations need the owner's explicit approval.
-- Tested on local Postgres 16 with stub auth.uid()/auth.role().
-- ============================================================================

-- ---------------------------------------------------------- economy_ledger --
create table if not exists public.economy_ledger (
  id            uuid primary key default gen_random_uuid(),
  user_id       uuid not null references auth.users(id) on delete cascade,
  client_ref    text not null,
  ts            timestamptz not null,
  delta         bigint not null,               -- signed naira (whole units)
  balance_after bigint not null,               -- SERVER-computed balance after this tx
  reason        text not null default '',      -- game reason, e.g. "danfo shift pay"
  source        text not null default 'game',  -- validated against nl_ledger_source_rules
  created_at    timestamptz not null default now(),
  seq           bigint generated always as identity  -- order by seq, not time
);
create index if not exists economy_ledger_user_id_idx on public.economy_ledger (user_id);
create index if not exists economy_ledger_user_ts_idx on public.economy_ledger (user_id, ts desc);

-- Legacy shape upgrades (browser 0009 / first 0019 had client_ref UNIQUE, no seq).
alter table public.economy_ledger
  drop constraint if exists economy_ledger_client_ref_key;
alter table public.economy_ledger
  add column if not exists seq bigint generated always as identity;
do $$
begin
  -- client_ref unique PER PLAYER, not globally.
  if not exists (select 1 from pg_constraint where conname = 'economy_ledger_user_ref_key') then
    alter table public.economy_ledger
      add constraint economy_ledger_user_ref_key unique (user_id, client_ref);
  end if;
end $$;
-- The opening balance exists once per player, ever.
create unique index if not exists economy_ledger_opening_once
  on public.economy_ledger (user_id) where source = 'opening_balance';

comment on table public.economy_ledger is
  'Unity 0002: append-only wallet log. balance_after is SERVER-computed '
  'by the RPCs in 0003; the client never decides a balance. Latest balance '
  'is the row with max seq for the user.';

-- ---------------------------------------------------------- economy_flags --
create table if not exists public.economy_flags (
  id         uuid primary key default gen_random_uuid(),
  user_id    uuid not null references auth.users(id) on delete cascade,
  kind       text not null check (kind in (
                 'tx_cap',            -- single transaction above the sane cap
                 'hourly_cap',        -- hourly gains above the sane cap
                 'negative_balance',  -- balance_after < 0
                 'balance_mismatch',  -- balance jump not explained by delta
                 'chain_break',        -- client hash chain failed verification
                 'clock_skew'         -- client ts far from server time
               )),
  detail     jsonb not null default '{}'::jsonb,
  created_at timestamptz not null default now()
);
create index if not exists economy_flags_user_id_idx on public.economy_flags (user_id);

-- ============================================================ Row Level Security
-- No direct writes for app users. Owners keep SELECT on their own rows;
-- every write goes through the RPCs in 0003 (security definer, bypass RLS)
-- or service_role (edge functions, bypass RLS).
alter table public.economy_ledger enable row level security;
alter table public.economy_flags  enable row level security;

drop policy if exists "owners insert" on public.economy_ledger;
drop policy if exists "owners insert" on public.economy_flags;

drop policy if exists "owners read" on public.economy_ledger;
create policy "owners read" on public.economy_ledger
  for select to authenticated using (auth.uid() = user_id);

drop policy if exists "owners read" on public.economy_flags;
create policy "owners read" on public.economy_flags
  for select to authenticated using (auth.uid() = user_id);

-- No INSERT/UPDATE/DELETE/TRUNCATE for app roles: without a policy the
-- REST insert fails, and the revoke makes the denial explicit.
grant select on public.economy_ledger to authenticated;
grant select on public.economy_flags to authenticated;
revoke insert, update, delete, truncate on public.economy_ledger from anon, authenticated;
revoke insert, update, delete, truncate on public.economy_flags  from anon, authenticated;

-- ----------------------------------------------- server-side source rules --
-- The single place that prices money. Amounts are whole naira.
-- daily_cap: max POSITIVE inflow per (user, source) per UTC day; null = no cap.
-- client_allowed = false: only service_role / security definer code writes it.
create table if not exists public.nl_ledger_source_rules (
  source         text primary key,
  min_amt        bigint not null,
  max_amt        bigint not null,
  daily_cap      bigint,
  client_allowed boolean not null default true,
  note           text not null default ''
);
alter table public.nl_ledger_source_rules enable row level security;
-- No policies: server config, not readable by app roles. The RPCs in 0003
-- run as security definer and read it fine; service_role bypasses RLS.
-- No table grants (server config, per the Oct 30 brief).
revoke all on public.nl_ledger_source_rules from anon, authenticated;

-- Client-claimable caps cut to what a hard-working day really earns.
insert into public.nl_ledger_source_rules (source, min_amt, max_amt, daily_cap, client_allowed, note) values
  ('job_payout',      1,           150000,     150000,    true,  'jobs, shifts, gigs, tips, fares — a hard day''s pay'),
  ('sale',            1,           50000,      150000,    true,  'small sales only; above ₦50k goes through nl_sale_verified'),
  ('purchase',       -25000000,   -1,          null,      true,  'any spend: food, travel, stock, rent, adverts'),
  ('fine',           -10000000,   -1,          null,      true,  'fines, taxes, court settlements'),
  ('loan',            1,           500000,      null,      false, 'SERVER ONLY: nl_loan_take writes it with the loan record'),
  ('loan_repay',     -500000,     -1,          null,      false, 'SERVER ONLY: nl_loan_repay matches it to an open loan'),
  ('gift',            1,           50000,      200000,    true,  'referral bonus, small gifts'),
  ('game',           -1000000,     20000,      20000,     true,  'legacy/default: uncategorized small tx (spend side kept wide)'),
  ('opening_balance', 0,           500000,     null,      true,  'one-time pre-B-4 wallet bootstrap (cutoff-gated, see 0003)'),
  ('pack_credit',     1,           1000000,   null,      false, 'SERVER ONLY: nl_pack_claim writes it after a paid pack'),
  ('starter_grant',   1,           1000000,   null,      false, 'SERVER ONLY: nl_start_life writes it (ON-1)'),
  ('family_stipend',  1,           100000,    null,      false, 'SERVER ONLY: scheduled stipend (ON-1)')
on conflict (source) do update set
  min_amt = excluded.min_amt, max_amt = excluded.max_amt,
  daily_cap = excluded.daily_cap, client_allowed = excluded.client_allowed,
  note = excluded.note;

-- -------------------------------- the opening-balance cutoff --
-- Stamped ONCE, the first time this migration runs. Re-runs keep the
-- original stamp. Accounts created before the stamp may post one
-- opening_balance row; newer accounts get starter money from ON-1's
-- server-side starter_grant instead. On a fresh Unity application the
-- stamp is "now", so no Unity account can ever use opening_balance —
-- which is correct: it is a browser-era bootstrap, kept only so the
-- contract stays identical where the tables already exist live.
create table if not exists public.nl_economy_config (
  key  text primary key,
  val  timestamptz not null,
  note text not null default ''
);
alter table public.nl_economy_config enable row level security;
revoke all on public.nl_economy_config from anon, authenticated;

insert into public.nl_economy_config (key, val, note)
select 'opening_cutoff', now(),
       'accounts created before this moment may post one opening_balance row'
where not exists (select 1 from public.nl_economy_config where key = 'opening_cutoff');

-- --------------------------------------------------------------- nl_loans --
-- Server-side loan book: every loan the game grants lives here. Client
-- 'loan' rows are rejected; only nl_loan_take() writes.
create table if not exists public.nl_loans (
  id           uuid primary key default gen_random_uuid(),
  user_id      uuid not null references auth.users(id) on delete cascade,
  principal    bigint not null check (principal > 0),
  balance      bigint not null check (balance >= 0),  -- total still owed, incl. interest
  lender       text not null default '',
  weeks        int not null default 4 check (weeks between 2 and 52),
  rate_weekly  numeric not null default 0.05,          -- server-priced: 5%/wk simple
  status       text not null default 'open' check (status in ('open', 'repaid', 'defaulted')),
  created_at   timestamptz not null default now(),
  repaid_at    timestamptz
);
create index if not exists nl_loans_user_idx on public.nl_loans (user_id, status);
alter table public.nl_loans enable row level security;
grant select on public.nl_loans to authenticated;
revoke insert, update, delete, truncate on public.nl_loans from anon, authenticated;
drop policy if exists "owners read" on public.nl_loans;
create policy "owners read" on public.nl_loans
  for select to authenticated using (auth.uid() = user_id);

-- ------------------------------------------------------------ nl_inventory --
-- Server-side seller inventory: what a player may sell above the ₦50k
-- client cap. Stocked only by server code (nl_inventory_grant,
-- service_role); spent only by nl_sale_verified, same-transaction.
create table if not exists public.nl_inventory (
  id         uuid primary key default gen_random_uuid(),
  user_id    uuid not null references auth.users(id) on delete cascade,
  item_ref   text not null,
  qty        bigint not null default 0 check (qty >= 0),
  meta       jsonb not null default '{}'::jsonb,
  updated_at timestamptz not null default now(),
  constraint nl_inventory_user_item_key unique (user_id, item_ref)
);
create index if not exists nl_inventory_user_idx on public.nl_inventory (user_id);
alter table public.nl_inventory enable row level security;
grant select on public.nl_inventory to authenticated;
revoke insert, update, delete, truncate on public.nl_inventory from anon, authenticated;
drop policy if exists "owners read" on public.nl_inventory;
create policy "owners read" on public.nl_inventory
  for select to authenticated using (auth.uid() = user_id);

-- --------------------------------------------------------- nl_item_prices --
-- Server price list: the ONLY prices nl_sale_verified trusts.
-- Seeded by the game/economy threads; unknown items cannot be sold above
-- the client cap.
create table if not exists public.nl_item_prices (
  item_ref       text primary key,
  max_unit_price bigint not null check (max_unit_price > 0),
  note           text not null default ''
);
alter table public.nl_item_prices enable row level security;
revoke all on public.nl_item_prices from anon, authenticated;
-- No policies: server config, read by security-definer RPCs only.

-- ------------------------------------------- server-side validation trigger
-- Runs on EVERY ledger insert, server-side, BEFORE the row lands. The RPC
-- in 0003 enforces stricter caps on the client path; this trigger still
-- watches rows written by service_role, where the RPC rules do not run.
-- Rows are flagged, never rejected.
create or replace function public.nl_validate_ledger_row()
returns trigger language plpgsql security definer
set search_path = public
as $$
declare
  c_max_single_tx   constant bigint := 100000000;   -- ₦100M per tx
  c_max_hourly_gain constant bigint := 200000000;  -- ₦200M per rolling hour
  v_hour_gain       bigint;
begin
  if abs(new.delta) > c_max_single_tx then
    insert into public.economy_flags (user_id, kind, detail)
    values (new.user_id, 'tx_cap',
            jsonb_build_object('delta', new.delta, 'reason', new.reason,
                               'client_ref', new.client_ref, 'cap', c_max_single_tx));
  end if;

  select coalesce(sum(delta), 0) into v_hour_gain
    from public.economy_ledger
    where user_id = new.user_id
      and delta > 0
      and ts > new.ts - interval '1 hour';
  if v_hour_gain + greatest(new.delta, 0) > c_max_hourly_gain then
    insert into public.economy_flags (user_id, kind, detail)
    values (new.user_id, 'hourly_cap',
            jsonb_build_object('hour_gain', v_hour_gain + greatest(new.delta, 0),
                               'reason', new.reason, 'cap', c_max_hourly_gain));
  end if;

  if new.balance_after < 0 then
    insert into public.economy_flags (user_id, kind, detail)
    values (new.user_id, 'negative_balance',
            jsonb_build_object('balance_after', new.balance_after,
                               'delta', new.delta, 'reason', new.reason));
  end if;

  if abs(extract(epoch from (new.ts - now()))) > 86400 then
    insert into public.economy_flags (user_id, kind, detail)
    values (new.user_id, 'clock_skew',
            jsonb_build_object('client_ts', new.ts, 'reason', new.reason));
  end if;

  return new;
end;
$$;

drop trigger if exists trg_economy_ledger_validate on public.economy_ledger;
create trigger trg_economy_ledger_validate
  before insert on public.economy_ledger
  for each row execute function public.nl_validate_ledger_row();

-- ============================================================================
-- CHECK QUERIES (run after applying, paste output in the commit).
--
-- -- 1. tables exist
-- select tablename from pg_tables where schemaname = 'public'
--   and tablename in ('economy_ledger', 'economy_flags', 'nl_ledger_source_rules',
--                     'nl_economy_config', 'nl_loans', 'nl_inventory', 'nl_item_prices');
--
-- -- 2. seq column + per-player client_ref unique + opening-once index
-- select column_name, data_type from information_schema.columns
--   where table_schema = 'public' and table_name = 'economy_ledger' and column_name = 'seq';
-- select conname, pg_get_constraintdef(oid) from pg_constraint
--   where conrelid = 'public.economy_ledger'::regclass and conname = 'economy_ledger_user_ref_key';
-- select indexname from pg_indexes where schemaname = 'public'
--   and indexname = 'economy_ledger_opening_once';
--
-- -- 3. source rules seeded with the 0019 caps
-- select source, min_amt, max_amt, daily_cap, client_allowed
--   from public.nl_ledger_source_rules order by source;
-- -- expect: job_payout 1..150000 cap 150000; sale 1..50000 cap 150000;
-- --         game -1000000..20000 cap 20000; loan/loan_repay client_allowed f
--
-- -- 4. no INSERT policies remain; RLS on
-- select tablename, policyname, cmd from pg_policies
--   where schemaname = 'public'
--     and tablename in ('economy_ledger', 'economy_flags', 'nl_loans', 'nl_inventory')
--   order by 1, 2;
-- -- expect: only "owners read" SELECT policies
--
-- -- 5. opening cutoff stamped
-- select key, val from public.nl_economy_config where key = 'opening_cutoff';
-- ============================================================================
