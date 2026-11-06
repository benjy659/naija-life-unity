-- ============================================================================
-- Naija Life Unity — Migration 0003: server-verified ledger RPCs
-- (Unity port of browser 0019 — same contracts, fresh Unity numbering,
--  standalone + re-runnable)
--
-- THE MONEY RULE: the server never trusts a client amount. Every money
-- write goes through one of these SECURITY DEFINER RPCs, which validate
-- every claim against SERVER-side state, recompute balance_after
-- server-side, and dedupe on (user_id, client_ref). Direct table writes
-- are revoked (0002). The client can MINT NOTHING through these paths:
--
--   nl_ledger_post_batch — the ONLY client path to write economy rows.
--     Per-user advisory lock FIRST (before any read): concurrent batches
--     for one player serialize, so no forked balances, no double
--     opening_balance, no daily-cap races. Retried client_refs are
--     answered first, before caps, and never move the running balance.
--     Server-owned ref namespaces ('pack_credit:', 'server:') are
--     rejected from client calls. Balance found by seq, never by time.
--   nl_flag_post         — client self-reported anomaly flags, kind
--     validated, detail size-capped, 200 flags/day per user.
--   nl_loan_take         — server-only loan drawdown: loan record +
--     ledger row together, server-priced 5%/week simple, max 3 open.
--   nl_loan_repay        — server-only repayment against an OPEN loan
--     of the caller, same transaction.
--   nl_sale_verified     — server-verified large sale (above the ₦50k
--     client cap): stock checked in nl_inventory, priced from the
--     SERVER price table, stock removed in the same transaction.
--   nl_inventory_grant   — service_role only: grant stock.
--   nl_pack_claim        — service_role only: claim a paid Flutterwave
--     pack credit. NOTE: reads public.payments (browser 0004/0013). The
--     function is written with scalar variables only (no %ROWTYPE), so
--     it CREATES cleanly even where payments does not exist yet — but it
--     only WORKS once the payments table exists. On the live project it
--     exists (browser); on a fresh project it must arrive with the
--     payments work.
--
-- GRANTS: player RPCs = authenticated may EXECUTE, explicitly revoked
-- from public AND anon. Server-only RPCs = service_role only, revoked
-- from public, anon AND authenticated.
--
-- STATUS: written 2026-11-05 (Unity backend builder). NOT APPLIED to the
-- live project — live migrations need the owner's explicit approval.
-- Tested on local Postgres 16 with stub auth.uid()/auth.role().
-- ============================================================================

-- ------------------------------------------- nl_ledger_post_batch --
-- The ONLY client path to write economy rows. Validates every row against
-- nl_ledger_source_rules, recomputes balance_after server-side, dedupes on
-- (user_id, client_ref). Returns a JSON array with one result object per
-- input row:
--   {"client_ref": "...", "ok": true, "balance_after": 12345}
--   {"client_ref": "...", "ok": false, "error": "..."}
create or replace function public.nl_ledger_post_batch(p_rows jsonb)
returns jsonb language plpgsql security definer set search_path = public as $$
declare
  v_uid      uuid := auth.uid();
  v_row      jsonb;
  v_ref      text;
  v_delta    bigint;
  v_reason   text;
  v_source   text;
  v_ts       timestamptz;
  v_min      bigint;
  v_max      bigint;
  v_cap      bigint;
  v_allowed  boolean;
  v_bal      bigint := 0;
  v_new      bigint;
  v_old      bigint;
  v_have_bal boolean := false;
  v_day_total bigint;
  v_has_rows boolean;
  v_cutoff   timestamptz;
  v_ucreated timestamptz;
  v_out      jsonb := '[]'::jsonb;
  v_res      jsonb;
begin
  if v_uid is null then raise exception 'sign-in required'; end if;
  -- The per-user lock is the FIRST statement, before any read.
  -- Concurrent batches for one player serialize: no forked balances,
  -- no double opening_balance, no daily-cap races.
  perform pg_advisory_xact_lock(hashtext(v_uid::text));

  if jsonb_typeof(p_rows) <> 'array' then raise exception 'p_rows must be a JSON array'; end if;
  if jsonb_array_length(p_rows) = 0 then return v_out; end if;
  if jsonb_array_length(p_rows) > 100 then raise exception 'batch too large (max 100 rows)'; end if;

  for v_row in select * from jsonb_array_elements(p_rows) loop
    begin
      v_ref := left(coalesce(v_row->>'client_ref', ''), 64);
      if v_ref = '' then raise exception 'client_ref required'; end if;
      -- Server-owned ref namespaces are never client-writable.
      if v_ref like 'pack_credit:%' or v_ref like 'server:%' then
        raise exception 'reserved client_ref prefix';
      end if;
      -- Retries are answered FIRST, before caps or bounds, and never
      -- move the running balance.
      select l.balance_after into v_old from public.economy_ledger l
        where l.user_id = v_uid and l.client_ref = v_ref;
      if found then
        v_out := v_out || jsonb_build_object('client_ref', v_ref, 'ok', true,
                   'balance_after', v_old, 'duplicate', true);
        continue;
      end if;
      begin
        v_delta := (v_row->>'delta')::bigint;
      exception when others then
        raise exception 'delta must be an integer';
      end;
      v_reason := left(coalesce(v_row->>'reason', ''), 120);
      v_source := coalesce(nullif(v_row->>'source', ''), 'game');
      begin
        v_ts := coalesce((v_row->>'ts')::timestamptz, now());
      exception when others then
        v_ts := now();
      end;
      -- Clock skew: never trust a client timestamp more than 24h out.
      if abs(extract(epoch from (v_ts - now()))) > 86400 then
        v_ts := now();
      end if;

      select r.min_amt, r.max_amt, r.daily_cap, r.client_allowed
        into v_min, v_max, v_cap, v_allowed
        from public.nl_ledger_source_rules r where r.source = v_source;
      if not found then raise exception 'unknown source: %', v_source; end if;
      if not v_allowed then
        raise exception 'server-only source: % (use the matching nl_* function)', v_source;
      end if;
      if v_delta < v_min or v_delta > v_max then
        raise exception 'amount % out of bounds for % (%..%)', v_delta, v_source, v_min, v_max;
      end if;

      -- One-time opening-balance bootstrap, pre-cutoff accounts only.
      -- The partial unique index is the hard guarantee; these checks
      -- give clean errors.
      if v_source = 'opening_balance' then
        select c.val into v_cutoff from public.nl_economy_config c where c.key = 'opening_cutoff';
        select u.created_at into v_ucreated from auth.users u where u.id = v_uid;
        if v_cutoff is null or v_ucreated is null or v_ucreated >= v_cutoff then
          raise exception 'opening balance is only for accounts created before the B-4 cutoff';
        end if;
        select exists(select 1 from public.economy_ledger where user_id = v_uid) into v_has_rows;
        if v_has_rows then raise exception 'already bootstrapped'; end if;
      end if;

      -- Daily positive-inflow cap per source (the anti-minting bound).
      -- Race-safe: the per-user lock is held for the whole batch.
      if v_cap is not null and v_delta > 0 then
        select coalesce(sum(delta), 0) into v_day_total
          from public.economy_ledger
          where user_id = v_uid and source = v_source and delta > 0
            and created_at >= date_trunc('day', now());
        if v_day_total + v_delta > v_cap then
          raise exception 'daily cap exceeded for % (%/%)', v_source, v_day_total + v_delta, v_cap;
        end if;
      end if;

      -- Server-computed running balance: the client's claimed balance is
      -- NEVER trusted. Latest row found by seq, not by time.
      if not v_have_bal then
        select balance_after into v_bal from public.economy_ledger
          where user_id = v_uid order by seq desc limit 1;
        if v_bal is null then v_bal := 0; end if;
        v_have_bal := true;
      end if;
      -- v_bal only moves AFTER the insert succeeds: PL/pgSQL variables are
      -- not rolled back when a row's block fails.
      v_new := v_bal + v_delta;

      insert into public.economy_ledger (user_id, client_ref, ts, delta, balance_after, reason, source)
        values (v_uid, v_ref, v_ts, v_delta, v_new, v_reason, v_source);

      -- Negative server balance: flag for review, never brick the player.
      if v_new < 0 then
        insert into public.economy_flags (user_id, kind, detail)
          values (v_uid, 'negative_balance',
                  jsonb_build_object('client_ref', v_ref, 'balance_after', v_new, 'source', v_source));
      end if;
      v_bal := v_new;

      v_res := jsonb_build_object('client_ref', v_ref, 'ok', true, 'balance_after', v_bal);
    exception when others then
      v_res := jsonb_build_object('client_ref', coalesce(v_ref, '?'), 'ok', false, 'error', SQLERRM);
    end;
    v_out := v_out || v_res;
  end loop;
  return v_out;
end $$;

-- ------------------------------------------------- nl_flag_post --
-- Client self-reported anomaly flags (the anticheat pipeline). Kind is
-- validated against the known enum, detail is size-capped, and each user
-- is rate-limited to 200 flags/day so a bad client cannot flood the table.
create or replace function public.nl_flag_post(p_kind text, p_detail jsonb)
returns uuid language plpgsql security definer set search_path = public as $$
declare
  v_uid uuid := auth.uid();
  v_id  uuid;
  v_n   int;
begin
  if v_uid is null then raise exception 'sign-in required'; end if;
  if p_kind not in ('tx_cap', 'hourly_cap', 'negative_balance',
                    'balance_mismatch', 'chain_break', 'clock_skew') then
    raise exception 'unknown flag kind';
  end if;
  if octet_length(coalesce(p_detail, '{}'::jsonb)::text) > 4096 then
    raise exception 'detail too large';
  end if;
  select count(*) into v_n from public.economy_flags
    where user_id = v_uid and created_at >= now() - interval '1 day';
  if v_n >= 200 then raise exception 'flag rate limit'; end if;
  insert into public.economy_flags (user_id, kind, detail)
    values (v_uid, p_kind, coalesce(p_detail, '{}'::jsonb))
    returning id into v_id;
  return v_id;
end $$;

-- ------------------------------------------------- nl_loan_take --
-- Server-only loan drawdown: writes the nl_loans record AND the ledger
-- row together, under the per-user lock. Terms are SERVER-priced (flat
-- 5%/week simple) — the client never sets amounts or rates. Client
-- 'loan' rows posted to nl_ledger_post_batch are rejected.
create or replace function public.nl_loan_take(p_amount bigint, p_lender text, p_weeks int)
returns jsonb language plpgsql security definer set search_path = public as $$
declare
  v_uid     uuid := auth.uid();
  v_lender  text := left(nullif(trim(coalesce(p_lender, '')), ''), 60);
  v_weeks   int  := coalesce(p_weeks, 4);
  v_total   bigint;
  v_weekly  bigint;
  v_open    int;
  v_bal     bigint := 0;
  v_loan_id uuid;
begin
  if v_uid is null then raise exception 'sign-in required'; end if;
  perform pg_advisory_xact_lock(hashtext(v_uid::text));

  if p_amount is null or p_amount < 1000 or p_amount > 500000 then
    raise exception 'loan amount must be 1000..500000';
  end if;
  if v_weeks < 2 or v_weeks > 52 then
    raise exception 'loan term must be 2..52 weeks';
  end if;
  if v_lender is null then v_lender := 'Lender'; end if;

  select count(*) into v_open from public.nl_loans
    where user_id = v_uid and status = 'open';
  if v_open >= 3 then raise exception 'too many open loans (max 3)'; end if;

  v_total  := p_amount + (p_amount * 5 * v_weeks) / 100;
  v_weekly := (v_total + v_weeks - 1) / v_weeks;

  insert into public.nl_loans (user_id, principal, balance, lender, weeks, rate_weekly, status)
    values (v_uid, p_amount, v_total, v_lender, v_weeks, 0.05, 'open')
    returning id into v_loan_id;

  select balance_after into v_bal from public.economy_ledger
    where user_id = v_uid order by seq desc limit 1;
  if v_bal is null then v_bal := 0; end if;
  insert into public.economy_ledger (user_id, client_ref, ts, delta, balance_after, reason, source)
    values (v_uid, 'server:loan:' || v_loan_id::text, now(), p_amount,
            v_bal + p_amount, 'loan from ' || v_lender, 'loan');

  return jsonb_build_object('ok', true, 'loan_id', v_loan_id,
    'principal', p_amount, 'total_due', v_total, 'weekly', v_weekly,
    'weeks', v_weeks, 'balance_after', v_bal + p_amount);
end $$;

-- ------------------------------------------------ nl_loan_repay --
-- Server-only repayment: pays down an OPEN loan of the caller and writes
-- the matching loan_repay ledger row in the same transaction. Client
-- 'loan_repay' rows posted to nl_ledger_post_batch are rejected.
create or replace function public.nl_loan_repay(p_loan_id uuid, p_amount bigint)
returns jsonb language plpgsql security definer set search_path = public as $$
declare
  v_uid  uuid := auth.uid();
  v_loan public.nl_loans%rowtype;
  v_pay  bigint;
  v_bal  bigint := 0;
begin
  if v_uid is null then raise exception 'sign-in required'; end if;
  perform pg_advisory_xact_lock(hashtext(v_uid::text));

  select * into v_loan from public.nl_loans where id = p_loan_id;
  if not found or v_loan.user_id <> v_uid then raise exception 'loan not found'; end if;
  if v_loan.status <> 'open' then raise exception 'loan is not open'; end if;
  if p_amount is null or p_amount < 1 then raise exception 'amount must be positive'; end if;
  v_pay := least(p_amount, v_loan.balance);

  select balance_after into v_bal from public.economy_ledger
    where user_id = v_uid order by seq desc limit 1;
  if v_bal is null then v_bal := 0; end if;
  insert into public.economy_ledger (user_id, client_ref, ts, delta, balance_after, reason, source)
    values (v_uid, 'server:loanrepay:' || gen_random_uuid()::text, now(), -v_pay,
            v_bal - v_pay, 'loan repayment — ' || v_loan.lender, 'loan_repay');

  update public.nl_loans
    set balance   = v_loan.balance - v_pay,
        status    = case when v_loan.balance - v_pay <= 0 then 'repaid' else 'open' end,
        repaid_at = case when v_loan.balance - v_pay <= 0 then now() else repaid_at end
    where id = p_loan_id;

  return jsonb_build_object('ok', true, 'paid', v_pay,
    'remaining', greatest(v_loan.balance - v_pay, 0),
    'balance_after', v_bal - v_pay);
end $$;

-- -------------------------------------------- nl_sale_verified --
-- Server-verified large sale (above the ₦50k client cap): checks the item
-- exists in the seller's nl_inventory, prices it from the SERVER price
-- table (never a client number), removes the stock and credits the seller
-- in the SAME transaction, under the per-user lock.
create or replace function public.nl_sale_verified(p_item_ref text, p_qty bigint)
returns jsonb language plpgsql security definer set search_path = public as $$
declare
  v_uid   uuid := auth.uid();
  v_ref   text := left(nullif(trim(coalesce(p_item_ref, '')), ''), 120);
  v_price bigint;
  v_have  bigint;
  v_total bigint;
  v_bal   bigint := 0;
begin
  if v_uid is null then raise exception 'sign-in required'; end if;
  perform pg_advisory_xact_lock(hashtext(v_uid::text));

  if v_ref is null then raise exception 'item_ref required'; end if;
  if p_qty is null or p_qty < 1 then raise exception 'qty must be positive'; end if;

  select max_unit_price into v_price from public.nl_item_prices where item_ref = v_ref;
  if v_price is null then raise exception 'item not priced: %', v_ref; end if;

  select qty into v_have from public.nl_inventory
    where user_id = v_uid and item_ref = v_ref;
  if v_have is null or v_have < p_qty then
    raise exception 'insufficient stock: have %, need %', coalesce(v_have, 0), p_qty;
  end if;
  v_total := p_qty * v_price;

  update public.nl_inventory
    set qty = qty - p_qty, updated_at = now()
    where user_id = v_uid and item_ref = v_ref;

  select balance_after into v_bal from public.economy_ledger
    where user_id = v_uid order by seq desc limit 1;
  if v_bal is null then v_bal := 0; end if;
  insert into public.economy_ledger (user_id, client_ref, ts, delta, balance_after, reason, source)
    values (v_uid, 'server:sale:' || gen_random_uuid()::text, now(), v_total,
            v_bal + v_total, 'sold ' || v_ref || ' x' || p_qty, 'sale');

  return jsonb_build_object('ok', true, 'total', v_total,
    'remaining_stock', v_have - p_qty, 'balance_after', v_bal + v_total);
end $$;

-- ------------------------------------------- nl_inventory_grant --
-- Server-only stock grant (for game server code / future edge functions).
-- execute: service_role only.
create or replace function public.nl_inventory_grant(p_user_id uuid, p_item_ref text, p_qty bigint)
returns jsonb language plpgsql security definer set search_path = public as $$
declare
  v_ref text := left(nullif(trim(coalesce(p_item_ref, '')), ''), 120);
begin
  if p_user_id is null then raise exception 'user required'; end if;
  if v_ref is null then raise exception 'item_ref required'; end if;
  if p_qty is null or p_qty < 1 then raise exception 'qty must be positive'; end if;
  insert into public.nl_inventory (user_id, item_ref, qty)
    values (p_user_id, v_ref, p_qty)
    on conflict (user_id, item_ref) do update
      set qty = public.nl_inventory.qty + excluded.qty, updated_at = now();
  return jsonb_build_object('ok', true);
end $$;

-- ------------------------------------------------ nl_pack_claim --
-- Server-only pack credit claim. Called by flw-verify with service_role
-- AFTER Flutterwave confirms a paid pack. Under the per-user lock: sets
-- credit_claimed_at where null and status='paid', inserts the pack_credit
-- row with the balance from the latest seq, returns the coin amount (0 if
-- already claimed / not payable). One transaction — no race.
-- NOTE: reads public.payments (browser 0004/0013). Created regardless —
-- the function uses only scalar variables (no %ROWTYPE), so PL/pgSQL
-- resolves the payments reference at first EXECUTION, not at creation:
-- this migration applies cleanly even where payments does not exist yet,
-- but the function only works once the payments table exists. Verify
-- payments exists on the live project before the first real claim.
create or replace function public.nl_pack_claim(p_payment_id uuid)
returns bigint language plpgsql security definer set search_path = public as $$
declare
  v_user_id uuid;
  v_status text;
  v_type text;
  v_credit_claimed_at timestamptz;
  v_metadata jsonb;
  v_tx_ref text;
  v_bal bigint := 0;
  v_amt bigint;
begin
  select user_id, status, type, credit_claimed_at, metadata, tx_ref
    into v_user_id, v_status, v_type, v_credit_claimed_at, v_metadata, v_tx_ref
    from public.payments where id = p_payment_id;
  if not found then return 0; end if;
  perform pg_advisory_xact_lock(hashtext(v_user_id::text));

  -- Re-read under the lock: a concurrent claim may have landed first.
  select user_id, status, type, credit_claimed_at, metadata, tx_ref
    into v_user_id, v_status, v_type, v_credit_claimed_at, v_metadata, v_tx_ref
    from public.payments where id = p_payment_id;
  if v_status <> 'paid' or v_type <> 'virtual'
     or v_credit_claimed_at is not null then
    return 0;
  end if;

  -- The pack's coin amount comes from the server's own payment metadata
  -- (written by flw-create-payment), never from the client.
  begin
    v_amt := nullif(v_metadata->>'virtualNGN', '')::bigint;
  exception when others then
    raise exception 'pack % has malformed virtualNGN metadata', p_payment_id;
  end;
  if v_amt is null or v_amt < 1 then return 0; end if;
  if v_amt > 1000000 then raise exception 'pack amount % out of bounds', v_amt; end if;

  update public.payments set credit_claimed_at = now()
    where id = p_payment_id and credit_claimed_at is null;
  if not found then return 0; end if;  -- lost the race; the winner paid it

  select balance_after into v_bal from public.economy_ledger
    where user_id = v_user_id order by seq desc limit 1;
  if v_bal is null then v_bal := 0; end if;
  insert into public.economy_ledger (user_id, client_ref, ts, delta, balance_after, reason, source)
    values (v_user_id, 'server:pack_credit:' || p_payment_id::text, now(), v_amt,
            v_bal + v_amt,
            'Top-up ' || left(coalesce(v_metadata->>'item', v_tx_ref, ''), 100),
            'pack_credit')
    on conflict (user_id, client_ref) do nothing;

  return v_amt;
end $$;

-- ------------------------------------------------------------------ grants --
-- Player RPCs: authenticated may CALL; nobody else.
revoke all on function public.nl_ledger_post_batch(jsonb) from public, anon;
revoke all on function public.nl_flag_post(text, jsonb) from public, anon;
revoke all on function public.nl_loan_take(bigint, text, integer) from public, anon;
revoke all on function public.nl_loan_repay(uuid, bigint) from public, anon;
revoke all on function public.nl_sale_verified(text, bigint) from public, anon;
grant execute on function public.nl_ledger_post_batch(jsonb) to authenticated;
grant execute on function public.nl_flag_post(text, jsonb) to authenticated;
grant execute on function public.nl_loan_take(bigint, text, integer) to authenticated;
grant execute on function public.nl_loan_repay(uuid, bigint) to authenticated;
grant execute on function public.nl_sale_verified(text, bigint) to authenticated;

-- Server-only RPCs: service_role only; revoked from public, anon AND
-- authenticated.
revoke all on function public.nl_pack_claim(uuid) from public, anon, authenticated;
revoke all on function public.nl_inventory_grant(uuid, text, bigint) from public, anon, authenticated;
grant execute on function public.nl_pack_claim(uuid) to service_role;
grant execute on function public.nl_inventory_grant(uuid, text, bigint) to service_role;

-- ============================================================================
-- CHECK QUERIES (run after applying, paste output in the commit).
--
-- -- 1. RPCs exist
-- select p.proname, pg_get_function_identity_arguments(p.oid)
--   from pg_proc p join pg_namespace n on n.oid = p.pronamespace
--   where n.nspname = 'public'
--     and p.proname in ('nl_ledger_post_batch', 'nl_flag_post', 'nl_loan_take',
--                       'nl_loan_repay', 'nl_sale_verified', 'nl_inventory_grant',
--                       'nl_pack_claim');
--
-- -- 2. grants: player RPCs callable by authenticated, NOT anon;
-- --    server-only RPCs callable by service_role, NOT anon/authenticated
-- select has_function_privilege('anon', 'public.nl_ledger_post_batch(jsonb)', 'execute') as anon_batch,
--        has_function_privilege('authenticated', 'public.nl_ledger_post_batch(jsonb)', 'execute') as auth_batch,
--        has_function_privilege('anon', 'public.nl_loan_take(bigint,text,integer)', 'execute') as anon_loan,
--        has_function_privilege('authenticated', 'public.nl_loan_take(bigint,text,integer)', 'execute') as auth_loan;
-- -- expect: f / t / f / t
-- select has_function_privilege('anon', 'public.nl_pack_claim(uuid)', 'execute') as anon_pack,
--        has_function_privilege('authenticated', 'public.nl_pack_claim(uuid)', 'execute') as auth_pack,
--        has_function_privilege('service_role', 'public.nl_pack_claim(uuid)', 'execute') as svc_pack;
-- -- expect: f / f / t
--
-- -- 3. mint rejections (as a signed-in user; each row returns ok:false)
-- select public.nl_ledger_post_batch(
--   '[{"client_ref":"t-sale-big","delta":60000,"reason":"mint","source":"sale","ts":"2026-11-05T12:00:00Z"},
--     {"client_ref":"t-loan","delta":100000,"reason":"mint","source":"loan","ts":"2026-11-05T12:00:00Z"},
--     {"client_ref":"pack_credit:x","delta":1000,"reason":"mint","source":"gift","ts":"2026-11-05T12:00:00Z"},
--     {"client_ref":"server:evil","delta":1000,"reason":"mint","source":"gift","ts":"2026-11-05T12:00:00Z"}]');
-- -- expect: every row ok:false (out of bounds / server-only source / reserved prefix)
--
-- -- 4. loan round-trip (as a signed-in user)
-- -- select public.nl_loan_take(50000, 'Test Lender', 4);  -- returns loan_id, total_due 60000
-- -- select public.nl_loan_repay('<loan_id>', 60000);      -- remaining 0, status repaid
-- -- select public.nl_loan_repay('<loan_id>', 1000);       -- raises 'loan is not open'
--
-- -- 5. verified sale needs stock + price (first two lines as postgres/service_role)
-- -- select public.nl_inventory_grant('<user_id>', 'test-item', 2);
-- -- insert into public.nl_item_prices values ('test-item', 75000, 'check');
-- -- select public.nl_sale_verified('test-item', 2);  -- total 150000, remaining_stock 0
-- -- select public.nl_sale_verified('test-item', 1);  -- raises 'insufficient stock'
--
-- -- 6. CONCURRENCY: two sessions, SAME signed-in user, run at once:
-- --   session A: select public.nl_ledger_post_batch('[{"client_ref":"conc-a","delta":100,"reason":"race","source":"game","ts":"2026-11-05T12:00:00Z"}]');
-- --   session B: select public.nl_ledger_post_batch('[{"client_ref":"conc-b","delta":200,"reason":"race","source":"game","ts":"2026-11-05T12:00:00Z"}]');
-- -- then: select balance_after from public.economy_ledger
-- --        where user_id = auth.uid() order by seq desc limit 1;
-- -- expect: previous balance + 300 exactly.
-- ============================================================================
