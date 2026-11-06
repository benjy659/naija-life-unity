-- Local test: server-verified ledger RPCs (migrations 0002 + 0003).
-- Run as the postgres superuser, AFTER setup.sql + migrations 0001-0004
-- AND 01_profiles_test.sql (user 1 holds 'TestName', user 2 'SecondUser').
-- Any failed assertion raises an exception and aborts the run.
--
-- Test users: A=1111 (opening balance), B=2222 (batch + mint),
--             C=3333 (caps + loans + sales), D=4444 (fresh, post-cutoff).

\echo '--- 02_ledger: batch happy path (user B) ---'
set role authenticated;
select set_config('app.current_user_id', '22222222-2222-2222-2222-222222222222', false);
do $$
declare
  v_out jsonb;
begin
  v_out := public.nl_ledger_post_batch(
    '[{"client_ref":"b-pay-1","delta":5000,"reason":"danfo shift pay","source":"job_payout","ts":"2026-11-05T12:00:00Z"},
      {"client_ref":"b-buy-1","delta":-1000,"reason":"food","source":"purchase","ts":"2026-11-05T12:00:00Z"},
      {"client_ref":"b-gift-1","delta":2000,"reason":"referral","source":"gift","ts":"2026-11-05T12:00:00Z"}]');
  if (v_out->0->>'ok')::boolean is not true or (v_out->0->>'balance_after')::bigint <> 5000 then
    raise exception 'FAIL: job_payout row wrong (got %)', v_out;
  end if;
  if (v_out->1->>'balance_after')::bigint <> 4000 then
    raise exception 'FAIL: purchase row balance wrong (got %)', v_out;
  end if;
  if (v_out->2->>'balance_after')::bigint <> 6000 then
    raise exception 'FAIL: gift row balance wrong (got %)', v_out;
  end if;
  raise notice 'PASS: batch happy path, server-computed balances';
end $$;

\echo '--- 02_ledger: mint rejections (user B) ---'
do $$
declare
  v_out jsonb;
begin
  v_out := public.nl_ledger_post_batch(
    '[{"client_ref":"t-sale-big","delta":60000,"reason":"mint","source":"sale","ts":"2026-11-05T12:00:00Z"},
      {"client_ref":"t-loan","delta":100000,"reason":"mint","source":"loan","ts":"2026-11-05T12:00:00Z"},
      {"client_ref":"pack_credit:x","delta":1000,"reason":"mint","source":"gift","ts":"2026-11-05T12:00:00Z"},
      {"client_ref":"server:evil","delta":1000,"reason":"mint","source":"gift","ts":"2026-11-05T12:00:00Z"},
      {"client_ref":"t-unknown","delta":1000,"reason":"mint","source":"nope","ts":"2026-11-05T12:00:00Z"}]');
  if (select count(*) from jsonb_array_elements(v_out) where (value->>'ok')::boolean) <> 0 then
    raise exception 'FAIL: every mint row must return ok:false (got %)', v_out;
  end if;
  -- Balance is unchanged by the rejected batch.
  if (select balance_after from public.economy_ledger
       where user_id = '22222222-2222-2222-2222-222222222222'
       order by seq desc limit 1) <> 6000 then
    raise exception 'FAIL: rejected batch moved the balance';
  end if;
  raise notice 'PASS: mint rejections (bounds / server-only / reserved prefix / unknown source)';
end $$;

\echo '--- 02_ledger: duplicate client_ref replay (user B) ---'
do $$
declare
  v_out jsonb;
begin
  v_out := public.nl_ledger_post_batch(
    '[{"client_ref":"b-pay-1","delta":5000,"reason":"danfo shift pay","source":"job_payout","ts":"2026-11-05T12:00:00Z"}]');
  if (v_out->0->>'ok')::boolean is not true
     or (v_out->0->>'duplicate')::boolean is not true
     or (v_out->0->>'balance_after')::bigint <> 5000 then
    raise exception 'FAIL: replay should answer the ORIGINAL balance_after (got %)', v_out;
  end if;
  if (select balance_after from public.economy_ledger
       where user_id = '22222222-2222-2222-2222-222222222222'
       order by seq desc limit 1) <> 6000 then
    raise exception 'FAIL: replay moved the running balance';
  end if;
  raise notice 'PASS: duplicate replay is idempotent';
end $$;

\echo '--- 02_ledger: daily cap (user C) ---'
select set_config('app.current_user_id', '33333333-3333-3333-3333-333333333333', false);
do $$
declare
  v_out jsonb;
begin
  -- ₦150,000 job_payout is exactly the daily cap: allowed.
  v_out := public.nl_ledger_post_batch(
    '[{"client_ref":"c-cap-1","delta":150000,"reason":"big day","source":"job_payout","ts":"2026-11-05T12:00:00Z"}]');
  if (v_out->0->>'ok')::boolean is not true then
    raise exception 'FAIL: at-cap payout should succeed (got %)', v_out;
  end if;
  -- One more naira of job_payout today: refused.
  v_out := public.nl_ledger_post_batch(
    '[{"client_ref":"c-cap-2","delta":1,"reason":"one more","source":"job_payout","ts":"2026-11-05T12:00:00Z"}]');
  if (v_out->0->>'ok')::boolean is not false
     or (v_out->0->>'error')::text not like '%daily cap exceeded%' then
    raise exception 'FAIL: over-cap payout should fail with daily cap (got %)', v_out;
  end if;
  raise notice 'PASS: daily cap enforced';
end $$;

\echo '--- 02_ledger: loan round-trip (user C) ---'
do $$
declare
  v_loan jsonb;
  v_repay jsonb;
  v_id uuid;
begin
  v_loan := public.nl_loan_take(50000, 'Test Lender', 4);
  if not (v_loan->>'ok')::boolean
     or (v_loan->>'total_due')::bigint <> 60000
     or (v_loan->>'weekly')::bigint <> 15000 then
    raise exception 'FAIL: loan terms wrong (got %)', v_loan;
  end if;
  v_id := (v_loan->>'loan_id')::uuid;
  if (v_loan->>'balance_after')::bigint <> 200000 then
    raise exception 'FAIL: loan drawdown balance wrong (got %)', v_loan;
  end if;
  v_repay := public.nl_loan_repay(v_id, 60000);
  if not (v_repay->>'ok')::boolean or (v_repay->>'remaining')::bigint <> 0 then
    raise exception 'FAIL: full repayment wrong (got %)', v_repay;
  end if;
  if (v_repay->>'balance_after')::bigint <> 140000 then
    raise exception 'FAIL: post-repayment balance wrong (got %)', v_repay;
  end if;
  begin
    perform public.nl_loan_repay(v_id, 1000);
    raise exception 'FAIL: repaying a repaid loan should raise';
  exception when others then
    if sqlerrm not like '%loan is not open%' then raise; end if;
  end;
  -- Fourth open loan is refused (max 3): take three, then a fourth.
  perform public.nl_loan_take(1000, 'L1', 2);
  perform public.nl_loan_take(1000, 'L2', 2);
  perform public.nl_loan_take(1000, 'L3', 2);
  begin
    perform public.nl_loan_take(1000, 'L4', 2);
    raise exception 'FAIL: fourth open loan should be refused';
  exception when others then
    if sqlerrm not like '%too many open loans%' then raise; end if;
  end;
  raise notice 'PASS: loan round-trip + max-3-open rule';
end $$;

\echo '--- 02_ledger: verified sale (user C) ---'
reset role;
-- Stock + price are server-seeded (superuser here; service_role on live).
do $$
begin
  perform public.nl_inventory_grant('33333333-3333-3333-3333-333333333333', 'test-item', 2);
  insert into public.nl_item_prices values ('test-item', 75000, 'ledger test');
end $$;
set role authenticated;
select set_config('app.current_user_id', '33333333-3333-3333-3333-333333333333', false);
do $$
declare
  v_sale jsonb;
begin
  v_sale := public.nl_sale_verified('test-item', 2);
  if not (v_sale->>'ok')::boolean
     or (v_sale->>'total')::bigint <> 150000
     or (v_sale->>'remaining_stock')::bigint <> 0 then
    raise exception 'FAIL: verified sale wrong (got %)', v_sale;
  end if;
  begin
    perform public.nl_sale_verified('test-item', 1);
    raise exception 'FAIL: sale without stock should raise';
  exception when others then
    if sqlerrm not like '%insufficient stock%' then raise; end if;
  end;
  begin
    perform public.nl_sale_verified('unpriced-item', 1);
    raise exception 'FAIL: sale of unpriced item should raise';
  exception when others then
    if sqlerrm not like '%item not priced%' then raise; end if;
  end;
  raise notice 'PASS: verified sale (server price + stock)';
end $$;

\echo '--- 02_ledger: pack claim (service_role only, user D) ---'
reset role;
-- User D must exist in auth.users BEFORE any ledger row for D (FK).
-- Created here, after the 0002 cutoff was stamped, so D stays a
-- post-cutoff account for the opening-balance test below.
insert into auth.users (id, created_at)
values ('44444444-4444-4444-4444-444444444444', now())
on conflict (id) do nothing;
do $$
begin
  insert into public.payments (id, user_id, status, type, metadata, tx_ref)
  values ('55555555-5555-5555-5555-555555555555',
          '44444444-4444-4444-4444-444444444444',
          'paid', 'virtual',
          '{"virtualNGN": "25000", "item": "coin pack"}'::jsonb, 'FLW-TEST-1'),
         ('66666666-6666-6666-6666-666666666666',
          '44444444-4444-4444-4444-444444444444',
          'pending', 'virtual',
          '{"virtualNGN": "25000"}'::jsonb, 'FLW-TEST-2');
end $$;
set role service_role;
do $$
declare
  v_amt bigint;
begin
  -- Paid pack credits once, from the server's own metadata.
  v_amt := public.nl_pack_claim('55555555-5555-5555-5555-555555555555');
  if v_amt <> 25000 then
    raise exception 'FAIL: pack claim should credit 25000 (got %)', v_amt;
  end if;
  -- Second claim of the same payment: 0 (idempotent, no double credit).
  v_amt := public.nl_pack_claim('55555555-5555-5555-5555-555555555555');
  if v_amt <> 0 then
    raise exception 'FAIL: second pack claim should return 0 (got %)', v_amt;
  end if;
  -- Unpaid payment and unknown payment: 0.
  v_amt := public.nl_pack_claim('66666666-6666-6666-6666-666666666666');
  if v_amt <> 0 then
    raise exception 'FAIL: pending pack claim should return 0 (got %)', v_amt;
  end if;
  v_amt := public.nl_pack_claim('00000000-0000-0000-0000-000000000000');
  if v_amt <> 0 then
    raise exception 'FAIL: unknown pack claim should return 0 (got %)', v_amt;
  end if;
  raise notice 'PASS: pack claim (once, idempotent, unpaid/unknown = 0)';
end $$;
reset role;
-- The credited ledger row is a server-owned pack_credit row.
do $$
begin
  if not exists (select 1 from public.economy_ledger
                 where user_id = '44444444-4444-4444-4444-444444444444'
                   and source = 'pack_credit'
                   and client_ref = 'server:pack_credit:55555555-5555-5555-5555-555555555555'
                   and delta = 25000 and balance_after = 25000) then
    raise exception 'FAIL: pack_credit ledger row missing or wrong';
  end if;
  raise notice 'PASS: pack_credit ledger row';
end $$;

\echo '--- 02_ledger: opening balance (user A pre-cutoff, user D post-cutoff) ---'
-- User A was created 30 days ago; the cutoff was stamped when 0002 ran.
select set_config('app.current_user_id', '11111111-1111-1111-1111-111111111111', false);
do $$
declare
  v_out jsonb;
begin
  v_out := public.nl_ledger_post_batch(
    '[{"client_ref":"a-open","delta":100000,"reason":"bootstrap","source":"opening_balance","ts":"2026-11-05T12:00:00Z"}]');
  if (v_out->0->>'ok')::boolean is not true
     or (v_out->0->>'balance_after')::bigint <> 100000 then
    raise exception 'FAIL: pre-cutoff opening balance should succeed (got %)', v_out;
  end if;
  v_out := public.nl_ledger_post_batch(
    '[{"client_ref":"a-open-2","delta":1000,"reason":"bootstrap","source":"opening_balance","ts":"2026-11-05T12:00:00Z"}]');
  if (v_out->0->>'ok')::boolean is not false
     or (v_out->0->>'error')::text not like '%already bootstrapped%' then
    raise exception 'FAIL: second opening balance should fail (got %)', v_out;
  end if;
  raise notice 'PASS: opening balance once-ever for pre-cutoff accounts';
end $$;
-- Fresh user D, created after the cutoff (inserted above, before the
-- pack-claim test): opening_balance is refused.
reset role;
set role authenticated;
select set_config('app.current_user_id', '44444444-4444-4444-4444-444444444444', false);
do $$
declare
  v_out jsonb;
begin
  v_out := public.nl_ledger_post_batch(
    '[{"client_ref":"d-open","delta":100000,"reason":"bootstrap","source":"opening_balance","ts":"2026-11-05T12:00:00Z"}]');
  if (v_out->0->>'ok')::boolean is not false
     or (v_out->0->>'error')::text not like '%before the B-4 cutoff%' then
    raise exception 'FAIL: post-cutoff opening balance should fail (got %)', v_out;
  end if;
  raise notice 'PASS: opening balance refused for post-cutoff accounts';
end $$;

\echo '--- 02_ledger: flags + grants ---'
do $$
declare
  v_id uuid;
begin
  v_id := public.nl_flag_post('clock_skew', '{"client_ts":"2020-01-01"}');
  if v_id is null then raise exception 'FAIL: flag post should return an id'; end if;
  begin
    perform public.nl_flag_post('not_a_kind', '{}');
    raise exception 'FAIL: unknown flag kind should raise';
  exception when others then
    if sqlerrm not like '%unknown flag kind%' then raise; end if;
  end;
  -- Grants: player RPCs callable by authenticated, NOT anon; server-only
  -- RPCs callable by service_role, NOT anon/authenticated.
  if has_function_privilege('anon', 'public.nl_ledger_post_batch(jsonb)', 'execute') then
    raise exception 'FAIL: anon must not execute nl_ledger_post_batch';
  end if;
  if not has_function_privilege('authenticated', 'public.nl_ledger_post_batch(jsonb)', 'execute') then
    raise exception 'FAIL: authenticated must execute nl_ledger_post_batch';
  end if;
  if has_function_privilege('authenticated', 'public.nl_pack_claim(uuid)', 'execute') then
    raise exception 'FAIL: authenticated must not execute nl_pack_claim';
  end if;
  if not has_function_privilege('service_role', 'public.nl_pack_claim(uuid)', 'execute') then
    raise exception 'FAIL: service_role must execute nl_pack_claim';
  end if;
  raise notice 'PASS: flags + RPC grants';
end $$;

\echo '--- 02_ledger: direct table writes denied, owners read own rows ---'
do $$
declare
  v_n int;
begin
  -- Direct REST-style insert is denied (no INSERT policy + revoked).
  begin
    insert into public.economy_ledger (user_id, client_ref, ts, delta, balance_after, reason, source)
    values ('44444444-4444-4444-4444-444444444444', 'direct-evil', now(), 999999999, 999999999, 'mint', 'game');
    raise exception 'FAIL: direct ledger insert should be denied';
  exception when insufficient_privilege then
    null;
  end;
  -- Own rows are readable (user D has exactly the 1 pack_credit row from
  -- the claim test above); other users' rows are invisible.
  select count(*) into v_n from public.economy_ledger
    where user_id = '44444444-4444-4444-4444-444444444444';
  if v_n <> 1 then raise exception 'FAIL: user D should have 1 ledger row (got %)', v_n; end if;
  select count(*) into v_n from public.economy_ledger
    where user_id = '22222222-2222-2222-2222-222222222222';
  if v_n <> 0 then raise exception 'FAIL: other user rows should be invisible (got %)', v_n; end if;
  raise notice 'PASS: RLS on ledger (read-own-only, no direct writes)';
end $$;
reset role;

\echo '--- 02_ledger: ALL PASS ---'
