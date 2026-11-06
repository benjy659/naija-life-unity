-- ============================================================================
-- Naija Life Unity — Migration 0001: profiles + username registry
-- (B-27 profiles + B-29 username lock, Unity port of browser 0001/0006/0008/
--  0010/0029 — same contracts, fresh Unity numbering, standalone + re-runnable)
--
-- WHAT THIS CREATES
--   public.profiles           — one row per player: id (auth.users), username,
--                                avatar_ref, device_id (reserved for the future
--                                one-device-per-account work), created_at.
--                                Username is set at sign-up (onboarding) via
--                                nl_create_profile() below — the client never
--                                writes this table directly.
--   public.username_registry  — global case-folded name registry: the ONLY
--                                source of identity. One row per claimed name.
--   nl_username_available()   — boolean availability check (anon-key safe).
--   nl_username_claim()       — atomic claim, requires an authenticated
--                                session (B-1); service_role exempt.
--   nl_create_profile()       — atomic onboarding: claim the name AND insert
--                                the profile row in one transaction, under the
--                                per-user lock. The single entry point for
--                                profile creation.
--   nl_guard_username()       — trigger: a profiles.username may only ever
--                                be a name the row's owner claimed in the
--                                registry. No role is exempt, service_role
--                                included.
--
-- AUTH MODEL: Supabase anonymous sign-in + email sign-in. The player signs
-- in (anonymous first, email linkable later), picks a username in
-- onboarding, and nl_create_profile() creates the profile. Until then the
-- player has an auth.users row and NO profile row — photon-auth (M5.1b)
-- refuses multiplayer without a profile.
--
-- MINIMUM-DATA RULE: profiles carry ONLY what the game needs — username,
-- avatar ref, creation time. No email, no DOB, no NIN, no location.
--
-- GRANTS (Supabase Oct 30, 2026 change): every table created in "public"
-- needs an explicit GRANT in the same migration or the Data API returns
-- "permission denied" despite correct RLS. Grants here are minimal:
-- profiles = owners read only (writes go through the RPC); registry =
-- RPC-only, no table grants.
--
-- STATUS: written 2026-11-05 (Unity backend builder). NOT APPLIED to the
-- live project — live migrations need the owner's explicit approval.
-- Tested on local Postgres 16 with stub auth.uid()/auth.role().
-- ============================================================================

-- ------------------------------------------------------------------ profiles --
create table if not exists public.profiles (
  id          uuid primary key references auth.users(id) on delete cascade,
  username    text not null,
  avatar_ref  text,                       -- e.g. "bust:v3:skin4:hair-dreads"
  device_id   text,                       -- reserved: one-device-per-account (future)
  created_at  timestamptz not null default now()
);
-- Shape upgrades for projects that already have profiles (browser 0001).
alter table public.profiles add column if not exists device_id text;

comment on table public.profiles is
  'Unity 0001 (B-27): one row per player. Created at sign-up via '
  'nl_create_profile() (claim + insert, atomic). The client never writes '
  'this table directly.';

alter table public.profiles enable row level security;

-- Owners read their own row. No INSERT/UPDATE/DELETE policies: every write
-- goes through nl_create_profile() (security definer, bypasses RLS).
drop policy if exists "owners read" on public.profiles;
create policy "owners read" on public.profiles
  for select to authenticated using (auth.uid() = id);

-- Explicit grants (Oct 30 rule): owners may read; no direct writes.
grant select on public.profiles to authenticated;
revoke insert, update, delete, truncate on public.profiles from anon, authenticated;

-- --------------------------------------------------------- username_registry --
create table if not exists public.username_registry (
  username_lower   text primary key,                 -- lower(trim(name))
  username_display text not null,                    -- original casing
  claimed_by       uuid,                             -- auth.uid() of claimer
  claimed_at       timestamptz not null default now()
);

comment on table public.username_registry is
  'Unity 0001 (B-29): global username registry — one row per claimed '
  'username. username_lower is the PK so claims are atomic and '
  'case-insensitive. Usernames are public by design (name tags, street '
  'card); the RPCs below expose only booleans, never identities.';

alter table public.username_registry enable row level security;
-- No policies: anon/authenticated have no direct access; RPCs are the only path.
-- No table grants (RPC-only table, per the Oct 30 brief).

-- ------------------------------------------------------- username RPCs (0008) --
create or replace function public.nl_username_available(p_name text)
returns boolean
language plpgsql
security definer
set search_path = public
as $$
declare
  k text := lower(trim(both from coalesce(p_name, '')));
begin
  -- Invalid names read as "not available" — the signup form owns the
  -- format message; the registry only answers taken-or-free.
  if k = '' or k !~ '^[a-z0-9_.\-]{3,16}$' then
    return false;
  end if;
  return not exists (
    select 1 from public.username_registry where username_lower = k
  );
end;
$$;

comment on function public.nl_username_available(text) is
  'Unity 0001: case-insensitive username availability. True = free to '
  'claim. Exposes only a boolean. Callable with the anon key.';

create or replace function public.nl_username_claim(p_name text)
returns boolean
language plpgsql
security definer
set search_path = public
as $$
declare
  k   text := lower(trim(both from coalesce(p_name, '')));
  d   text := trim(both from coalesce(p_name, ''));
  uid uuid := auth.uid();
begin
  -- B-1 (browser 0010): no anonymous claims. The client sends the player's
  -- session JWT; without it auth.uid() is null and the claim is rejected
  -- before any insert happens. service_role (admin tooling / backfills)
  -- is exempt.
  if uid is null and coalesce(auth.role(), '') <> 'service_role' then
    raise exception 'nl_username_claim: authentication required';
  end if;
  if k = '' or k !~ '^[a-z0-9_.\-]{3,16}$' then
    return false;
  end if;
  insert into public.username_registry (username_lower, username_display, claimed_by)
  values (k, d, uid)
  on conflict (username_lower) do nothing;
  -- FOUND is true only when THIS call inserted the row — the PK decides the
  -- race, so two simultaneous claims for one name can't both return true.
  return found;
end;
$$;

comment on function public.nl_username_claim(text) is
  'Unity 0001 (B-1): atomic username reservation. Rejects requests with '
  'no authenticated user. True = this caller owns the name now; false = '
  'already taken (or invalid).';

-- ---------------------------------------------------- nl_create_profile --
-- The single entry point for profile creation (Unity onboarding): claim
-- the name AND insert the profile row in one transaction, under the
-- per-user lock. Returns {ok, username} or raises a clean error.
--   'authentication required' — no session JWT
--   'username taken'          — another player owns the name
--   'invalid username'        — fails the 3..16 [a-z0-9_.\\-] format rule
--   'profile already exists'  — this account already has a profile
create or replace function public.nl_create_profile(p_username text)
returns jsonb
language plpgsql
security definer
set search_path = public
as $$
declare
  v_uid uuid := auth.uid();
  k     text := lower(trim(both from coalesce(p_username, '')));
  d     text := trim(both from coalesce(p_username, ''));
  v_ok  boolean;
begin
  if v_uid is null then
    raise exception 'authentication required';
  end if;
  -- Per-user lock first: a double-submit (or two tabs) cannot create two
  -- profiles or claim two names for one account.
  perform pg_advisory_xact_lock(hashtext(v_uid::text));

  if exists (select 1 from public.profiles where id = v_uid) then
    raise exception 'profile already exists';
  end if;
  if k = '' or k !~ '^[a-z0-9_.\-]{3,16}$' then
    raise exception 'invalid username';
  end if;

  -- Claim first: the PK is the arbiter of the race.
  insert into public.username_registry (username_lower, username_display, claimed_by)
  values (k, d, v_uid)
  on conflict (username_lower) do nothing;
  if not found then
    raise exception 'username taken';
  end if;

  insert into public.profiles (id, username)
  values (v_uid, d);

  return jsonb_build_object('ok', true, 'username', d);
end;
$$;

comment on function public.nl_create_profile(text) is
  'Unity 0001 (B-27): atomic onboarding — claim the username in the '
  'registry AND insert the profiles row, one transaction. The only way a '
  'profile row is created.';

-- ------------------------------------------------- nl_guard_username (0029) --
-- A profiles.username may only ever be a name the row's owner claimed in
-- username_registry. Fires on INSERT (any set name "differs") and on UPDATE
-- only when the name actually changes. NO exemption for service_role.
create or replace function public.nl_guard_username()
returns trigger
language plpgsql
security definer
set search_path = public
as $$
begin
  if new.username is not null
     and (tg_op = 'INSERT' or new.username is distinct from old.username) then
    if not exists (
      select 1
        from public.username_registry
       where username_lower = lower(btrim(new.username))
         and claimed_by = new.id
    ) then
      raise exception
        'nl_guard_username: username "%" is not claimed by this user in username_registry', new.username;
    end if;
  end if;
  return new;
end;
$$;

comment on function public.nl_guard_username() is
  'Unity 0001 (B-29): trigger guard — a profiles.username INSERT/change is '
  'refused unless username_registry holds the name with claimed_by = the '
  'profile id. No role is exempt, service_role included.';

-- Trigger functions need no EXECUTE grant; make sure none leaks.
revoke all on function public.nl_guard_username() from public, anon, authenticated;

drop trigger if exists nl_guard_username on public.profiles;
create trigger nl_guard_username
  before insert or update on public.profiles
  for each row execute function public.nl_guard_username();

-- Unique index on lower(username): two profiles can never hold the same
-- name, case-insensitively. Pre-flight: refuse to build it over duplicate
-- live names (resolve duplicates first — keep the registry owner's row).
do $$
declare
  v_dup text;
begin
  select string_agg(username_lower || ' x' || n::text, ', ')
    into v_dup
    from (select lower(username) as username_lower, count(*) as n
            from public.profiles
           where username is not null
           group by lower(username)
          having count(*) > 1) d;
  if v_dup is not null then
    raise exception
      'Unity 0001: duplicate profiles.username (case-insensitive): % — resolve the duplicates (keep the registry owner''s row) before applying', v_dup;
  end if;
end $$;

create unique index if not exists profiles_username_lower_uidx
  on public.profiles (lower(username))
  where username is not null;

comment on index public.profiles_username_lower_uidx is
  'Unity 0001 (B-29): two profiles can never hold the same username, '
  'case-insensitively.';

-- ------------------------------------------------------------------- grants --
-- Postgres grants EXECUTE to PUBLIC on every new function by default; revoke
-- that first so the grants below are the only way in.
revoke all on function public.nl_username_available(text) from public;
revoke all on function public.nl_username_claim(text) from public;
revoke all on function public.nl_create_profile(text) from public;
grant execute on function public.nl_username_available(text) to anon, authenticated;
grant execute on function public.nl_username_claim(text) to anon, authenticated;
-- nl_create_profile: authenticated only — it creates an account record, so
-- an anon-key-only call (no JWT) must not reach it. The function body also
-- rejects a null auth.uid().
revoke all on function public.nl_create_profile(text) from anon;
grant execute on function public.nl_create_profile(text) to authenticated;

-- ============================================================================
-- CHECK QUERIES (run after applying, paste output in the commit).
--
-- -- 1. tables + index + trigger exist
-- select tablename from pg_tables where schemaname = 'public'
--   and tablename in ('profiles', 'username_registry');
-- select indexname from pg_indexes where schemaname = 'public'
--   and indexname = 'profiles_username_lower_uidx';
-- select tgname from pg_trigger
--   where tgrelid = 'public.profiles'::regclass and tgname = 'nl_guard_username';
--
-- -- 2. grants: profiles readable by authenticated, NOT writable directly;
-- --    registry has no table grants at all
-- select has_table_privilege('authenticated', 'public.profiles', 'select') as prof_read,
--        has_table_privilege('authenticated', 'public.profiles', 'insert') as prof_insert,
--        has_table_privilege('authenticated', 'public.username_registry', 'select') as reg_read;
-- -- expect: t / f / f
--
-- -- 3. behaviour (as two signed-in test users A and B):
-- --   select public.nl_username_available('testname');          -> true
-- --   select public.nl_create_profile('testname');              -> {ok:true}
-- --   select public.nl_username_available('testname');          -> false
-- --   as B: select public.nl_create_profile('testname');        -> raises 'username taken'
-- --   as A: update profiles set username='other' where id = A;  -> raises nl_guard_username
-- --   as A: select public.nl_create_profile('second');          -> raises 'profile already exists'
-- ============================================================================
