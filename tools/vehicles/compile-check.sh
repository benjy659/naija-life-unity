#!/usr/bin/env bash
# Compile check and logic test run for the M3 vehicle C# without Unity (cloud
# threads have no Editor). Same idea as tools/oshodi/compile-check.sh:
#   1. NaijaLife.Vehicles.Logic + its 16 EditMode tests, compiled against real
#      NUnit 3.5 and RUN with NUnitLite (pure C#, no engine).
#   2. Assets/Scripts/Vehicles, Verification/M3*, Player/IPlayerCameraTarget.cs
#      against UnityEngine 2021.3 module reference assemblies, with small stubs
#      for the Input System, PlayerCamera/PlayerController and NLPrompts
#      (stubs-runtime.cs.txt).
#   3. Assets/Editor/Vehicles against the UnityEditor 2018.1 reference assembly.
# It catches typos, missing usings and wrong types. It does NOT prove the code
# compiles in Unity 6 or that it drives well: Ted's Editor run (inbox IN-19)
# is still the check. mcs rejects `foreach (var (a, b) in ...)`; avoid it.
#
#   apt-get install -y mono-mcs mono-runtime   (once)
#   bash tools/vehicles/compile-check.sh
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
CACHE="${UNITY_REF_CACHE:-/tmp/unity-ref-cache}"
OUT="$CACHE/vehicles"
mkdir -p "$OUT"; cd "$CACHE"
get() { [ -d "$2" ] || { curl -sSL -o "$2.nupkg" "$1" && unzip -q -o "$2.nupkg" -d "$2"; }; }
get https://api.nuget.org/v3-flatcontainer/unityengine.modules/2021.3.33/unityengine.modules.2021.3.33.nupkg ue
get https://api.nuget.org/v3-flatcontainer/unity3d.unityeditor/2018.1.6-f1/unity3d.unityeditor.2018.1.6-f1.nupkg ed
get https://api.nuget.org/v3-flatcontainer/nunit/3.5.0/nunit.3.5.0.nupkg nunit
get https://api.nuget.org/v3-flatcontainer/nunitlite/3.5.0/nunitlite.3.5.0.nupkg nunitlite
cp "$ROOT/tools/vehicles/stubs-runtime.cs.txt" "$OUT/stubs.cs"
NUNIT=nunit/lib/net45/nunit.framework.dll
LITE=nunitlite/lib/net45/nunitlite.dll
REFS=$(ls ue/lib/net45/*.dll | sed 's/^/-r:/' | tr '\n' ' ')
M="mcs -nologo -langversion:experimental"
V="$ROOT/Assets/Scripts/Vehicles"

# 1. Pure logic + tests, run.
$M -target:library -out:"$OUT/NaijaLife.Vehicles.Logic.dll" "$V"/Logic/*.cs
cat > "$OUT/run.cs" <<'CS'
public static class RunTests { public static int Main(string[] a) { return new NUnitLite.AutoRun(typeof(NaijaLife.Vehicles.Tests.VehicleLogicTests).Assembly).Execute(a); } }
CS
$M -out:"$OUT/vehicles-tests.exe" -r:"$NUNIT" -r:"$LITE" -r:"$OUT/NaijaLife.Vehicles.Logic.dll" \
  "$ROOT"/Assets/Tests/EditMode/Vehicles/*.cs "$OUT/run.cs"
cp "$NUNIT" "$LITE" "$OUT/"
(cd "$OUT" && mono vehicles-tests.exe --noresult --labels=Off) | grep -E "Test Count|Overall result|Failed :|^\s+[0-9]+\)" || true
(cd "$OUT" && mono vehicles-tests.exe --noresult --labels=Off >/dev/null)

# 2. Runtime scripts.
$M -target:library -nowarn:0414,0169,0649 -out:"$OUT/vehicles.dll" $REFS -r:"$OUT/NaijaLife.Vehicles.Logic.dll" \
  "$OUT/stubs.cs" "$ROOT/Assets/Scripts/Player/IPlayerCameraTarget.cs" \
  "$V"/*.cs "$ROOT"/Assets/Scripts/Verification/M3*.cs

# 3. Editor scripts. The reference UnityEditor is 2018.1, older than a few
# APIs Unity 6 has; those errors (ALLOW) are expected, anything else fails.
ALLOW='SaveAsPrefabAsset'
set +e
LOG=$($M -target:library -nowarn:0414,0169,0649 -out:"$OUT/vehicles-editor.dll" $REFS -r:ed/lib/UnityEditor.dll \
  -r:"$OUT/NaijaLife.Vehicles.Logic.dll" -r:"$OUT/vehicles.dll" "$ROOT"/Assets/Editor/Vehicles/*.cs 2>&1)
set -e
BAD=$(printf '%s\n' "$LOG" | grep 'error CS' | grep -vE "$ALLOW" || true)
if [ -n "$BAD" ]; then printf '%s\n' "$BAD"; echo "FAIL: editor code"; exit 1; fi
printf '%s\n' "$LOG" | grep -q 'error CS' && echo "note: only newer-than-2018.1 editor APIs missing ($ALLOW), as expected"
echo "PASS: M3 logic tests pass; vehicle runtime and editor code compile against the reference assemblies."
