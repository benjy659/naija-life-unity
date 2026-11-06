# Naija Life Unity — Supabase backend tests

Local-only verification. **Nothing here touches the live Supabase project.**

## What is covered

### SQL tests — `sql/` + `run_sql_tests.sh`

Run: `./run_sql_tests.sh` (from this directory or anywhere; it resolves
the repo root itself).

Prerequisites: local PostgreSQL 16 (`apt install postgresql-16`,
`pg_ctlcluster 16 main start`). The runner drops/creates a scratch
database `naija_unity_test`, so it needs a postgres superuser (`su postgres`).

What it does, in order:

1. `setup.sql` — stub `auth` schema (`auth.users`, `auth.uid()` /
   `auth.role()` backed by session settings) + the `anon`,
   `authenticated`, `service_role` roles + a minimal stub `payments`
   table (only the columns `nl_pack_claim` reads).
2. Applies migrations `0001`–`0004` **twice** — the second pass proves
   they are re-runnable (idempotent) on an already-migrated database.
3. `01_profiles_test.sql` — availability rules, anon claim rejected,
   `nl_create_profile` happy path + single profile, claim race between
   two users, guard trigger refusing unclaimed inserts, unique index
   present, RLS read-own-only + no direct writes.
4. `02_ledger_test.sql` — batch happy path with server-computed
   balances; mint rejections (out-of-bounds, server-only source,
   reserved `pack_credit:`/`server:` prefixes, unknown source);
   duplicate `client_ref` replay is idempotent; daily cap enforced;
   loan round-trip (5%/week, max 3 open); verified sale (server price +
   stock); pack claim once + idempotent + unpaid/unknown = 0;
   opening balance once-ever for pre-cutoff accounts, refused for
   post-cutoff accounts; flags + RPC grants; RLS on the ledger.
5. `03_street_card_test.sql` — lookup by id, unknown id,
   no-session denied.
6. Concurrency check — two parallel psql sessions post `+100` and
   `+200` for the same user; the final balance must be exactly
   `+300` (per-user advisory lock serializes writers; no forked
   balance).

Any failed assertion raises an exception and aborts the run with a
`FAIL` line. A full pass ends with `ALL SQL TESTS PASSED`.

The runner stages world-readable copies of the SQL files under
`/tmp/naija-sql-tests.XXXXXX` (the `postgres` OS user cannot traverse
`/home/hatch`) and removes them on exit.

### Deno tests — Edge Functions

Run from `supabase/functions/`:

```sh
deno test --allow-net --allow-env photon-auth/photon-auth.test.ts
```

`photon-auth.test.ts` exercises the Photon custom-auth handler with a
mocked Supabase client (no live project, no network): valid JWT +
profile → 200 `{UserId, Nickname}`; valid JWT without a profile → 403
`no_profile`; invalid/missing token → 401; bearer header and
query-string token both work; non-POST/GET → 405.

`deno.json` in `supabase/functions/` registers the
`@supabase/supabase-js` npm dependency (`deno install` resolves it);
`deno.lock` pins the version. `node_modules/` is gitignored.

## Notes

- The local stub `auth.uid()` reads the session setting
  `app.current_user_id`; tests simulate callers with
  `SET ROLE authenticated` + `set_config(...)`. RLS is genuinely
  enforced after `SET ROLE` (superuser bypass does not apply).
- The `payments` stub is local-only. The live project already has the
  real `payments` table (browser 0004/0013) — verify it still exists
  before the first real `nl_pack_claim` call (see `supabase/README.md`
  live checklist).
