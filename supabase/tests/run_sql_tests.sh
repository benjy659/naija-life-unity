#!/usr/bin/env bash
# Naija Life Unity — local SQL verification for the Supabase backend.
# Applies migrations 0001-0004 to a scratch Postgres 16 database (twice:
# the second apply proves re-runnability), then runs the SQL unit tests,
# including a two-session concurrency check.
#
# Usage: ./run_sql_tests.sh
# Requires: local Postgres 16 (service postgresql) + psql. Nothing here
# touches the live Supabase project — all local, all scratch.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
MIGRATIONS="$REPO_ROOT/supabase/migrations"
TESTS="$REPO_ROOT/supabase/tests/sql"
DB="naija_unity_test"
PSQL="su postgres -c"

# The postgres OS user cannot traverse /home/hatch, so stage world-readable
# copies of the SQL files under /tmp for psql -f (scratch only; the repo
# files remain the source of truth).
STAGE="$(mktemp -d /tmp/naija-sql-tests.XXXXXX)"
cp "$TESTS/setup.sql" "$TESTS/01_profiles_test.sql" "$TESTS/02_ledger_test.sql" \
   "$TESTS/03_street_card_test.sql" "$STAGE/"
cp "$MIGRATIONS"/0001_unity_profiles.sql "$MIGRATIONS"/0002_unity_ledger.sql \
   "$MIGRATIONS"/0003_unity_ledger_rpcs.sql "$MIGRATIONS"/0004_unity_street_card.sql "$STAGE/"
chmod -R a+rX "$STAGE"
trap 'rm -rf "$STAGE"' EXIT
MIGRATIONS="$STAGE"
TESTS="$STAGE"

echo "== drop/create scratch database $DB =="
$PSQL "psql -c \"DROP DATABASE IF EXISTS $DB;\" -c \"CREATE DATABASE $DB;\"" >/dev/null

run() { # run <label> <file> — fails the script on any psql error
  echo "== $1 =="
  local out
  if ! out=$($PSQL "psql -d $DB -v ON_ERROR_STOP=1 -f \"$2\"" 2>&1); then
    echo "$out" | sed 's/^/  /'
    echo "  FAIL: $1 (psql error)"
    exit 1
  fi
  echo "$out" | grep -E "PASS|FAIL|ERROR|NOTICE" | sed 's/^/  /' || true
}

run "setup (stub auth schema + roles)" "$TESTS/setup.sql"

# Apply migrations TWICE — the second pass proves re-runnability.
for pass in 1 2; do
  echo "== apply migrations (pass $pass) =="
  for f in "$MIGRATIONS"/0001_unity_profiles.sql \
           "$MIGRATIONS"/0002_unity_ledger.sql \
           "$MIGRATIONS"/0003_unity_ledger_rpcs.sql \
           "$MIGRATIONS"/0004_unity_street_card.sql; do
    echo "  -- $(basename "$f")"
    if ! $PSQL "psql -d $DB -v ON_ERROR_STOP=1 -q -f \"$f\"" > /tmp/naija_mig.out 2>&1; then
      echo "  FAIL: $(basename "$f")"
      cat /tmp/naija_mig.out | sed 's/^/  /'
      exit 1
    fi
  done
done

run "01 profiles" "$TESTS/01_profiles_test.sql"
run "02 ledger" "$TESTS/02_ledger_test.sql"
run "03 street card" "$TESTS/03_street_card_test.sql"

echo "== concurrency: two sessions, same user, +100 and +200 'game' =="
# User B (2222) balance after 02 is 6000; two parallel batches must land
# exactly +300 (previous balance + 300), proving the per-user lock
# serializes concurrent writers instead of forking the balance.
# (SQL lives in files, not inline -c strings: nested quoting mangles JSON.)
cat > "$STAGE/conc_a.sql" <<'SQL'
SET ROLE authenticated;
SELECT set_config('app.current_user_id', '22222222-2222-2222-2222-222222222222', false);
SELECT public.nl_ledger_post_batch('[{"client_ref":"conc-a","delta":100,"reason":"race","source":"game","ts":"2026-11-05T12:00:00Z"}]');
SQL
cat > "$STAGE/conc_b.sql" <<'SQL'
SET ROLE authenticated;
SELECT set_config('app.current_user_id', '22222222-2222-2222-2222-222222222222', false);
SELECT public.nl_ledger_post_batch('[{"client_ref":"conc-b","delta":200,"reason":"race","source":"game","ts":"2026-11-05T12:00:00Z"}]');
SQL
chmod a+r "$STAGE/conc_a.sql" "$STAGE/conc_b.sql"
$PSQL "psql -d $DB -t -f \"$STAGE/conc_a.sql\"" > /tmp/naija_conc_a.out 2>&1 &
PID_A=$!
$PSQL "psql -d $DB -t -f \"$STAGE/conc_b.sql\"" > /tmp/naija_conc_b.out 2>&1 &
PID_B=$!
wait $PID_A; wait $PID_B
grep -q '"ok": true' /tmp/naija_conc_a.out || { echo "  FAIL: session A"; cat /tmp/naija_conc_a.out; exit 1; }
grep -q '"ok": true' /tmp/naija_conc_b.out || { echo "  FAIL: session B"; cat /tmp/naija_conc_b.out; exit 1; }
BAL=$($PSQL "psql -d $DB -t -A -c \"SELECT balance_after FROM public.economy_ledger WHERE user_id = '22222222-2222-2222-2222-222222222222' ORDER BY seq DESC LIMIT 1;\"" | tr -d '[:space:]')
if [ "$BAL" != "6300" ]; then
  echo "  FAIL: expected balance 6300 after concurrent batches, got $BAL"
  exit 1
fi
echo "  PASS: concurrent batches serialized, balance 6300 exactly"

echo ""
echo "ALL SQL TESTS PASSED"
