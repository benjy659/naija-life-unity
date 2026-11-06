#!/usr/bin/env bash
# Compile check and NLMoney test run for the HUD/phone C# without Unity (cloud
# threads have no Editor). Same method as tools/jobs/compile-check.sh:
#   1. NaijaLife.UI.Core (pure C#, NLMoney) + its EditMode tests, compiled
#      against real NUnit 3.5 and RUN with NUnitLite.
#   2. Assets/Scripts/UI against UnityEngine 2021.3 module reference assemblies
#      (UI Toolkit included), with stubs for the Input System, URP, Player and
#      Vehicles (stubs-runtime.cs.txt).
#   3. Assets/Editor/UI against the UnityEditor 2018.1 reference assembly.
# APIs newer than the reference dlls (ALLOW) are expected errors; anything
# else fails. It catches typos, missing usings and wrong types. It does NOT
# prove the code compiles in Unity 6 or that the HUD looks right: Ted's
# Editor run (inbox IN-16, IN-20, IN-21) is still the check.
#
#   apt-get install -y mono-mcs mono-runtime   (once)
#   bash tools/ui/compile-check.sh
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
CACHE="${UNITY_REF_CACHE:-/tmp/unity-ref-cache}"
OUT="$CACHE/ui"
mkdir -p "$OUT"; cd "$CACHE"
get() { [ -d "$2" ] || { curl -sSL -o "$2.nupkg" "$1" && unzip -q -o "$2.nupkg" -d "$2"; }; }
get https://api.nuget.org/v3-flatcontainer/unityengine.modules/2021.3.33/unityengine.modules.2021.3.33.nupkg ue
get https://api.nuget.org/v3-flatcontainer/unity3d.unityeditor/2018.1.6-f1/unity3d.unityeditor.2018.1.6-f1.nupkg ed
get https://api.nuget.org/v3-flatcontainer/nunit/3.5.0/nunit.3.5.0.nupkg nunit
get https://api.nuget.org/v3-flatcontainer/nunitlite/3.5.0/nunitlite.3.5.0.nupkg nunitlite
cp "$ROOT/tools/ui/stubs-runtime.cs.txt" "$OUT/stubs.cs"
NUNIT=nunit/lib/net45/nunit.framework.dll
LITE=nunitlite/lib/net45/nunitlite.dll
REFS=$(ls ue/lib/net45/*.dll | sed 's/^/-r:/' | tr '\n' ' ')
M="mcs -nologo -langversion:experimental"
U="$ROOT/Assets/Scripts/UI"
# APIs Unity 6 has but the reference dlls do not (none needed so far).
ALLOW="__none__"

# 1. Pure core + tests, run.
$M -target:library -out:"$OUT/NaijaLife.UI.Core.dll" "$U"/Core/*.cs
cat > "$OUT/run.cs" <<'CS'
public static class RunTests { public static int Main(string[] a) { return new NUnitLite.AutoRun(typeof(NaijaLife.UI.Tests.NLMoneyTests).Assembly).Execute(a); } }
CS
$M -out:"$OUT/ui-tests.exe" -r:"$NUNIT" -r:"$LITE" -r:"$OUT/NaijaLife.UI.Core.dll" \
  "$ROOT"/Assets/Tests/EditMode/UI/*.cs "$OUT/run.cs"
cp "$NUNIT" "$LITE" "$OUT/"
(cd "$OUT" && mono ui-tests.exe --noresult --labels=Off) | grep -E "Test Count|Overall result|Failed :|^\s+[0-9]+\)" || true
(cd "$OUT" && mono ui-tests.exe --noresult --labels=Off >/dev/null)

check() { # name, then mcs args
  local name="$1"; shift
  set +e; local log; log=$($M -target:library -nowarn:0414,0169,0649,0162 "$@" 2>&1); set -e
  local bad; bad=$(printf '%s\n' "$log" | grep 'error CS' | grep -vE "$ALLOW" || true)
  if [ -n "$bad" ]; then printf '%s\n' "$bad"; echo "FAIL: $name"; exit 1; fi
  if printf '%s\n' "$log" | grep -q 'error CS'; then echo "note: $name only misses newer-than-reference APIs ($ALLOW), as expected"; fi
}

# 2. Runtime UI (Assembly-CSharp in Unity). The Editor-only fake wallet is
# under #if UNITY_EDITOR; compile both ways so the player build is checked too.
check "UI runtime (player build)" -out:"$OUT/ui-runtime.dll" $REFS -r:"$OUT/NaijaLife.UI.Core.dll" "$OUT/stubs.cs" "$U"/*.cs
check "UI runtime (Editor)" -define:UNITY_EDITOR -out:"$OUT/ui-runtime-editor.dll" $REFS -r:"$OUT/NaijaLife.UI.Core.dll" "$OUT/stubs.cs" "$U"/*.cs

# 3. Editor setup menu, against the Editor-build runtime dll.
check "UI Editor setup" -out:"$OUT/ui-editor.dll" $REFS -r:ed/lib/UnityEditor.dll -r:"$OUT/NaijaLife.UI.Core.dll" \
  -r:"$OUT/ui-runtime-editor.dll" "$OUT/stubs.cs" "$ROOT"/Assets/Editor/UI/*.cs
echo "PASS: NLMoney tests pass; UI runtime (player and Editor builds) and the setup menu compile against the reference assemblies."
