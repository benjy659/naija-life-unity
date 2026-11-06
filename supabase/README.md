# Naija Life Unity — Supabase backend

Backend for the Unity rebuild. **Same Supabase project as the browser
version** (`naija-life`, ref `jdumcxvqjtgnbeuvlufq`, ca-central-1) — the
browser version is frozen, but its backend design carries over. These
migrations are standalone + re-runnable: every object is created with
`IF NOT EXISTS` / `CREATE OR REPLACE`, so they apply cleanly whether or
not the browser migrations landed live.

## Contents

| Path | What it is |
|---|---|
| `migrations/0001_unity_profiles.sql` | `profiles` + `username_registry` + claim/available/create_profile + username guard trigger (B-27 + B-29) |
| `migrations/0002_unity_ledger.sql` | `economy_ledger`, `economy_flags`, source rules, config, loans, inventory, prices + validation trigger |
| `migrations/0003_unity_ledger_rpcs.sql` | Server-verified money RPCs: `nl_ledger_post_batch`, `nl_flag_post`, `nl_loan_take`, `nl_loan_repay`, `nl_sale_verified`, `nl_inventory_grant`, `nl_pack_claim` |
| `migrations/0004_unity_street_card.sql` | `nl_street_card_by_id` (M5.1f — **owner-gated**, see below) |
| `functions/photon-auth/` | Photon Fusion 2 custom-auth webhook (M5.1b): verifies the Supabase JWT, requires a profile |
| `functions/_shared/auth.ts` | JWT verification + profile resolution helper (no service_role key) |
| `tests/` | Local verification: SQL tests (Postgres 16) + Deno tests (photon-auth) |

## The money rule (non-negotiable)

The server never trusts a client amount. Every money write goes through
a SECURITY DEFINER RPC that validates against server-side state,
recomputes `balance_after` server-side, and dedupes on
`(user_id, client_ref)`. Direct table writes are revoked. Fusion never
decides who owns what or how much money anyone has.

## Auth model

- **Anonymous sign-in + email sign-in.** The player signs in anonymously,
  picks a username in onboarding, `nl_create_profile()` creates the
  profile atomically (claim + insert). Email can be linked to the same
  account later (Supabase supports linking identities).
- **Photon anonymous auth is OFF**: every player must present a Supabase
  JWT to `photon-auth`. No guest path.
- **No service_role key anywhere in this backend.** The Edge Function
  verifies JWTs with the anon key and reads profiles with the caller's
  own JWT, so RLS still applies.

## Live dashboard settings (apply once, by hand — not in a migration)

In the Supabase dashboard for project `jdumcxvqjtgnbeuvlufq`:
1. Authentication → Providers → **Anonymous: enabled**.
2. Authentication → Providers → **Email: enabled** (confirm-email ON for
   the WebGL host; set Site URL + redirect URLs to the game's hosted
   origin once it exists).
3. Authentication → Policies: nothing to change — the migrations carry
   the RLS + grants.

## Apply checklist (OWNER-GATED — nothing here touches live without Benjamin's explicit OK)

Apply in order, from the repo root, with the Supabase CLI linked
(`supabase link --project-ref jdumcxvqjtgnbeuvlufq`):

```sh
# 1. Apply the migrations (never paste into the dashboard SQL editor for
#    files this size — the CLI is the supported path).
supabase db push --project-ref jdumcxvqjtgnbeuvlufq

# 2. Deploy the Edge Function.
supabase functions deploy photon-auth --project-ref jdumcxvqjtgnbeuvlufq

# 3. Verify the function answers (expect 401 without a token):
curl -s -o /dev/null -w '%{http_code}\n' \
  https://jdumcxvqjtgnbeuvlufq.supabase.co/functions/v1/photon-auth \
  -X POST -H "Content-Type: application/json" -d '{}'

# 4. Run the check queries at the end of each migration file and paste
#    the output in the apply commit.
```

**Before step 1, verify the live project still has the `payments` table**
(browser 0004/0013): `nl_pack_claim` (0003) only *works* once `payments`
exists — it creates cleanly without it, but a real pack claim would fail
at first execution. Check: `select count(*) from public.payments;`
should succeed on live. If the table is gone, hold 0003's pack-claim
use (not the migration itself) until the payments work lands.

Then, in the **Photon dashboard** (manual, once): the Fusion app →
Custom Authentication → webhook URL
`https://jdumcxvqjtgnbeuvlufq.supabase.co/functions/v1/photon-auth`
(POST). No secret needed — the Supabase JWT is the credential.

**Migration 0004 (`nl_street_card_by_id`, M5.1f) is written now but
applies only with the owner's separate OK** (TASKS.md M5.1: Claude
reviews M5.1f before application). If the owner approves 0001–0003 but
not 0004, apply 0001–0003 and hold 0004 — they are independent files.

## What is NOT in this package (separate owner-gated work)

- **B-38 player-to-player transfers** (migration 0040 in the browser
  repo): spec approved for BUILD, applying needs Benjamin's SEPARATE
  OK. Not written here — it arrives as its own gated package.
- **M4.4 shift RPCs** (`nl_job_shifts`, `nl_job_trips`, `nl_shift_start`,
  `nl_shift_trip`, `nl_shift_end`, server-only source `job_shift`):
  owned by the M4.4 workstream. This package seeds the base source
  rules only; M4.4's migration inserts the `job_shift` row.
- **Property (P-0)** and **business/rent sources**: arrive with the
  property/business work.
- **Supabase Pro ($25/mo)**: direction confirmed; purchase needs his yes
  on exact terms. Everything here runs on the Free plan.
- **Frozen browser DB items** (migration 0021 `player_reports`
  conflict, 0033/0036/0037/0038, 0039): untouched — separate thread.

## Grants (Supabase Oct 30, 2026 change)

From Oct 30, 2026, new tables in `public` get NO automatic Data API
access. Every table in these migrations carries an explicit GRANT in the
same file (or an explicit revoke where the table is RPC/server-only).
Rule: grant only what the client access pattern needs; RPC-only tables
get no table grants. If a future migration adds a table, add its grants
in the same file.

## Local verification (how this package was tested)

No live project was touched. See `tests/README.md`:
- SQL: local Postgres 16 + stub `auth` schema; migrations applied
  twice (re-runnable check); unit tests for profiles, username race,
  ledger caps, mint rejections, loan round-trip, verified sale,
  two-session concurrency.
- Edge Function: Deno `deno check` + `deno test` with a mocked auth
  client (no network, no live project).
