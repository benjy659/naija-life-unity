-- Local test: street card by id (migration 0004, M5.1f).
-- Run as the postgres superuser, AFTER setup.sql + migrations 0001-0004
-- AND 01_profiles_test.sql (user 1 'TestName', user 2 'SecondUser').
-- Any failed assertion raises an exception and aborts the run.

\echo '--- 03_street_card: lookup by id ---'
set role authenticated;
select set_config('app.current_user_id', '11111111-1111-1111-1111-111111111111', false);
do $$
declare
  v_card jsonb;
begin
  v_card := public.nl_street_card_by_id('22222222-2222-2222-2222-222222222222');
  if not (v_card->>'ok')::boolean or v_card->>'username' <> 'SecondUser' then
    raise exception 'FAIL: street card should return SecondUser (got %)', v_card;
  end if;
  if v_card->>'member_since' is null then
    raise exception 'FAIL: street card should include member_since';
  end if;
  v_card := public.nl_street_card_by_id('00000000-0000-0000-0000-000000000000');
  if (v_card->>'ok')::boolean or v_card->>'error' <> 'no_such_player' then
    raise exception 'FAIL: unknown id should return no_such_player (got %)', v_card;
  end if;
  -- Grants: anon cannot execute, authenticated can.
  if has_function_privilege('anon', 'public.nl_street_card_by_id(uuid)', 'execute') then
    raise exception 'FAIL: anon must not execute nl_street_card_by_id';
  end if;
  if not has_function_privilege('authenticated', 'public.nl_street_card_by_id(uuid)', 'execute') then
    raise exception 'FAIL: authenticated must execute nl_street_card_by_id';
  end if;
  raise notice 'PASS: street card by id';
end $$;

\echo '--- 03_street_card: no session is denied ---'
reset role;
select set_config('app.current_user_id', '', false);
do $$
declare
  v_card jsonb;
begin
  v_card := public.nl_street_card_by_id('22222222-2222-2222-2222-222222222222');
  if (v_card->>'ok')::boolean or v_card->>'error' <> 'denied' then
    raise exception 'FAIL: no-session lookup should be denied (got %)', v_card;
  end if;
  raise notice 'PASS: no-session denied';
end $$;

\echo '--- 03_street_card: ALL PASS ---'
