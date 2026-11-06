-- Local test setup: stub Supabase Auth schema + roles.
-- Run BEFORE the migrations, as the postgres superuser, in a scratch DB.
-- Mirrors the browser team's b21_* local-test pattern.
--
-- The stub provides:
--   auth.users(id, created_at)           — the table profiles/ledger FK to
--   auth.uid() / auth.role()             — backed by session settings
--                                          app.current_user_id / app.current_role
--   roles anon, authenticated, service_role (nologin; tests use SET ROLE)
--
-- Tests simulate a signed-in user with:
--   SET ROLE authenticated;
--   SELECT set_config('app.current_user_id', '<uuid>', false);
-- and an anon-key-only caller with:
--   SET ROLE anon;   -- (no user id set)

create schema if not exists auth;

create table if not exists auth.users (
  id uuid primary key,
  created_at timestamptz not null default now()
);

create or replace function auth.uid() returns uuid
language sql stable as $$
  select nullif(current_setting('app.current_user_id', true), '')::uuid;
$$;

create or replace function auth.role() returns text
language sql stable as $$
  select coalesce(nullif(current_setting('app.current_role', true), ''), 'authenticated');
$$;

-- Supabase's built-in roles (created here for the local scratch DB).
do $$
begin
  if not exists (select 1 from pg_roles where rolname = 'anon') then
    create role anon nologin;
  end if;
  if not exists (select 1 from pg_roles where rolname = 'authenticated') then
    create role authenticated nologin;
  end if;
  if not exists (select 1 from pg_roles where rolname = 'service_role') then
    create role service_role nologin;
  end if;
end $$;

-- Test users (created_at in the past: all are "pre-cutoff" only if the
-- cutoff is stamped after them — the cutoff is stamped when 0002 runs,
-- so these users are pre-cutoff. The opening_balance test relies on that
-- only for the POSITIVE case; the negative case uses a fresh user).
insert into auth.users (id, created_at) values
  ('11111111-1111-1111-1111-111111111111', now() - interval '30 days'),
  ('22222222-2222-2222-2222-222222222222', now() - interval '30 days'),
  ('33333333-3333-3333-3333-333333333333', now() - interval '30 days')
on conflict (id) do nothing;

-- Minimal stub of the browser payments table (browser 0004/0013) — only
-- the columns nl_pack_claim reads. The live project already has the real
-- table; this stub exists so the claim path can be exercised locally.
create table if not exists public.payments (
  id uuid primary key,
  user_id uuid not null,
  status text not null,
  type text not null,
  metadata jsonb not null default '{}'::jsonb,
  tx_ref text,
  credit_claimed_at timestamptz
);
