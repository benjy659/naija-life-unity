-- Local test: profiles + username registry (migration 0001).
-- Run as the postgres superuser, AFTER setup.sql + migrations 0001-0004.
-- Each section simulates callers with SET ROLE + set_config, then asserts.
-- Any failed assertion raises an exception and aborts the run.

\echo '--- 01_profiles: availability + claim ---'
set role authenticated;
select set_config('app.current_user_id', '11111111-1111-1111-1111-111111111111', false);
do $$
begin
  -- Availability: free name -> true; invalid names -> false.
  if not public.nl_username_available('testname') then
    raise exception 'FAIL: fresh name should be available';
  end if;
  if public.nl_username_available('ab') then
    raise exception 'FAIL: 2-char name should be unavailable';
  end if;
  if public.nl_username_available('BAD NAME!') then
    raise exception 'FAIL: name with space/bang should be unavailable';
  end if;
  if public.nl_username_available('') then
    raise exception 'FAIL: empty name should be unavailable';
  end if;
  raise notice 'PASS: availability rules';
end $$;

\echo '--- 01_profiles: claim requires a session ---'
-- Anon-key-only caller (no JWT): claim must raise.
set role anon;
select set_config('app.current_user_id', '', false);
do $$
declare
  v_err text;
begin
  begin
    perform public.nl_username_claim('anonname');
  exception when others then
    v_err := sqlerrm;
  end;
  if v_err is null or v_err not like '%authentication required%' then
    raise exception 'FAIL: anon claim should raise authentication required (got %)', coalesce(v_err, 'no error');
  end if;
  raise notice 'PASS: anon claim rejected';
end $$;
-- Availability stays callable without a session (boolean only).
do $$
begin
  if not public.nl_username_available('testname') then
    raise exception 'FAIL: availability should work without a session';
  end if;
  raise notice 'PASS: availability without session';
end $$;

\echo '--- 01_profiles: create_profile (atomic claim + insert) ---'
set role authenticated;
select set_config('app.current_user_id', '11111111-1111-1111-1111-111111111111', false);
do $$
declare
  v_res jsonb;
begin
  v_res := public.nl_create_profile('TestName');
  if not (v_res->>'ok')::boolean or v_res->>'username' <> 'TestName' then
    raise exception 'FAIL: create_profile should return ok + TestName (got %)', v_res;
  end if;
  -- Name is now taken (case-insensitive).
  if public.nl_username_available('testname') or public.nl_username_available('TESTNAME') then
    raise exception 'FAIL: claimed name should be unavailable in any case';
  end if;
  -- Profile row exists with the display casing.
  if not exists (select 1 from public.profiles where id = '11111111-1111-1111-1111-111111111111' and username = 'TestName') then
    raise exception 'FAIL: profile row missing after create_profile';
  end if;
  -- Second profile for the same account is refused.
  begin
    perform public.nl_create_profile('secondname');
    raise exception 'FAIL: second profile should be refused';
  exception when others then
    if sqlerrm not like '%profile already exists%' then raise; end if;
  end;
  raise notice 'PASS: create_profile happy path + single profile';
end $$;

\echo '--- 01_profiles: claim race — second user cannot take the name ---'
select set_config('app.current_user_id', '22222222-2222-2222-2222-222222222222', false);
do $$
begin
  begin
    perform public.nl_create_profile('testname');
    raise exception 'FAIL: second user taking the name should be refused';
  exception when others then
    if sqlerrm not like '%username taken%' then raise; end if;
  end;
  -- But the second user CAN claim a different free name.
  if not (public.nl_create_profile('SecondUser')->>'ok')::boolean then
    raise exception 'FAIL: second user should create a profile with a free name';
  end if;
  raise notice 'PASS: claim race + second user';
end $$;

\echo '--- 01_profiles: guard trigger refuses unclaimed renames ---'
-- Direct write as superuser (bypasses RLS) with a name the user never claimed.
reset role;
do $$
begin
  begin
    insert into public.profiles (id, username)
    values ('33333333-3333-3333-3333-333333333333', 'StolenName');
    raise exception 'FAIL: unclaimed profile insert should be refused by the guard';
  exception when others then
    if sqlerrm not like '%nl_guard_username%' then raise; end if;
  end;
  raise notice 'PASS: guard refuses unclaimed insert';
end $$;
-- The unique index fires when the name IS claimed by the inserter but
-- another profile already holds it in a different case.
-- The unique index fires when the name IS claimed by the inserter but
-- another profile already holds it in a different case. That state is
-- unreachable through the RPC by design (the claim race refuses it
-- first), so here we assert the index EXISTS as the backstop — the
-- guard + claim race above already cover the behaviour.
do $$
begin
  perform set_config('app.current_user_id', '33333333-3333-3333-3333-333333333333', false);
  set role authenticated;
  if not public.nl_username_claim('thirdname') then
    raise exception 'FAIL: user 3 should claim thirdname';
  end if;
  reset role;
  if not exists (select 1 from pg_indexes where schemaname = 'public'
                 and indexname = 'profiles_username_lower_uidx') then
    raise exception 'FAIL: profiles_username_lower_uidx missing';
  end if;
  raise notice 'PASS: guard + unique index present';
end $$;

\echo '--- 01_profiles: RLS — owners read own row, cannot write directly ---'
set role authenticated;
select set_config('app.current_user_id', '11111111-1111-1111-1111-111111111111', false);
do $$
declare
  v_n int;
begin
  -- Own row is readable.
  select count(*) into v_n from public.profiles where id = '11111111-1111-1111-1111-111111111111';
  if v_n <> 1 then raise exception 'FAIL: owner should read own profile (got %)', v_n; end if;
  -- Another player's row is invisible.
  select count(*) into v_n from public.profiles where id = '22222222-2222-2222-2222-222222222222';
  if v_n <> 0 then raise exception 'FAIL: other player profile should be invisible (got %)', v_n; end if;
  -- Direct insert is denied (no INSERT policy).
  begin
    insert into public.profiles (id, username)
    values ('11111111-1111-1111-1111-111111111111', 'directwrite');
    raise exception 'FAIL: direct profile insert should be denied by RLS';
  exception when insufficient_privilege then
    -- expected: RLS policy violation raises insufficient_privilege
    null;
  end;
  raise notice 'PASS: RLS read-own-only + no direct writes';
end $$;
reset role;

\echo '--- 01_profiles: ALL PASS ---'
