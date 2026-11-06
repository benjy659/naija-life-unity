#!/usr/bin/env bash
# Runs every outside-Unity check in one go: each tools/<area>/compile-check.sh
# (C# against Unity reference assemblies with mono, plus the pure-logic tests)
# and tools/check-net-boundary.sh. Prints one PASS/FAIL line per area and exits
# non-zero if any failed. These catch typos and wrong types early; they do NOT
# replace Ted's Unity 6 Editor runs and WebGL captures.
#
#   apt-get install -y mono-mcs mono-runtime   (once)
#   bash tools/check-all.sh            # all areas
#   bash tools/check-all.sh ui player  # only some
set -uo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
command -v mcs >/dev/null || { echo "mcs not found: apt-get install -y mono-mcs mono-runtime"; exit 2; }
if [ $# -gt 0 ]; then AREAS=("$@"); else
  AREAS=(); for f in "$ROOT"/tools/*/compile-check.sh; do AREAS+=("$(basename "$(dirname "$f")")"); done; fi
LOGS="${TMPDIR:-/tmp}/nl-check-all"; mkdir -p "$LOGS"
fail=0
for a in "${AREAS[@]}"; do
  s="$ROOT/tools/$a/compile-check.sh"
  if [ ! -f "$s" ]; then echo "FAIL  $a (no tools/$a/compile-check.sh)"; fail=1; continue; fi
  if bash "$s" >"$LOGS/$a.log" 2>&1; then echo "PASS  $a"
  else echo "FAIL  $a (log: $LOGS/$a.log)"; tail -15 "$LOGS/$a.log" | sed 's/^/      /'; fail=1; fi
done
if [ $# -eq 0 ] && [ -f "$ROOT/tools/check-net-boundary.sh" ]; then
  if bash "$ROOT/tools/check-net-boundary.sh" >"$LOGS/net-boundary.log" 2>&1; then echo "PASS  net-boundary"
  else echo "FAIL  net-boundary"; tail -15 "$LOGS/net-boundary.log" | sed 's/^/      /'; fail=1; fi
fi
[ $fail -eq 0 ] && echo "ALL PASS" || echo "SOME FAILED"
exit $fail
