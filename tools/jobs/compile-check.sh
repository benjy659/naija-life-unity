#!/usr/bin/env bash
# Compile check and Jobs.Core test run for the M4 C# without Unity (cloud
# threads have no Editor). Same method as tools/vehicles/compile-check.sh:
#   1. NaijaLife.Jobs.Core (pure C#) + its EditMode tests, compiled against
#      real NUnit 3.5 and RUN with NUnitLite.
#   2. Assets/Scripts/Jobs, Jobs/Conductor, Economy and Player/PlayerNeeds.cs
#      against UnityEngine 2021.3 module reference assemblies, with stubs for
#      the Input System, the UI kit (NLPrompts, NLToasts, NLWallet, NLJobHud),
#      NetBootstrap/SupabaseSession and PlayerController/PlayerCamera
#      (stubs-runtime.cs.txt).
#   3. Assets/Editor/Jobs against the UnityEditor 2018.1 reference assembly.
# It catches typos, missing usings and wrong types. It does NOT prove the code
# compiles in Unity 6 or that the shift plays well: Ted's Editor run (inbox
# IN-18) is still the check. mcs rejects `foreach (var (a, b) in ...)`.
#
#   apt-get install -y mono-mcs mono-runtime   (once)
#   bash tools/jobs/compile-check.sh
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
CACHE="${UNITY_REF_CACHE:-/tmp/unity-ref-cache}"
OUT="$CACHE/jobs"
mkdir -p "$OUT"; cd "$CACHE"
get() { [ -d "$2" ] || { curl -sSL -o "$2.nupkg" "$1" && unzip -q -o "$2.nupkg" -d "$2"; }; }
get https://api.nuget.org/v3-flatcontainer/unityengine.modules/2021.3.33/unityengine.modules.2021.3.33.nupkg ue
get https://api.nuget.org/v3-flatcontainer/unity3d.unityeditor/2018.1.6-f1/unity3d.unityeditor.2018.1.6-f1.nupkg ed
get https://api.nuget.org/v3-flatcontainer/nunit/3.5.0/nunit.3.5.0.nupkg nunit
get https://api.nuget.org/v3-flatcontainer/nunitlite/3.5.0/nunitlite.3.5.0.nupkg nunitlite
cp "$ROOT/tools/jobs/stubs-runtime.cs.txt" "$OUT/stubs.cs"
NUNIT=nunit/lib/net45/nunit.framework.dll
LITE=nunitlite/lib/net45/nunitlite.dll
REFS=$(ls ue/lib/net45/*.dll | sed 's/^/-r:/' | tr '\n' ' ')
M="mcs -nologo -langversion:experimental"
J="$ROOT/Assets/Scripts/Jobs"

# 1. Pure core + tests, run.
$M -target:library -out:"$OUT/NaijaLife.Jobs.Core.dll" "$J"/Core/*.cs
cat > "$OUT/run.cs" <<'CS'
public static class RunTests { public static int Main(string[] a) { return new NUnitLite.AutoRun(typeof(NaijaLife.Jobs.Tests.ShiftRulesTests).Assembly).Execute(a); } }
CS
$M -out:"$OUT/jobs-tests.exe" -r:"$NUNIT" -r:"$LITE" -r:"$OUT/NaijaLife.Jobs.Core.dll" \
  "$ROOT"/Assets/Tests/EditMode/Jobs/*.cs "$OUT/run.cs"
cp "$NUNIT" "$LITE" "$OUT/"
(cd "$OUT" && mono jobs-tests.exe --noresult --labels=Off) | grep -E "Test Count|Overall result|Failed :|^\s+[0-9]+\)" || true
(cd "$OUT" && mono jobs-tests.exe --noresult --labels=Off >/dev/null)

# 2. Runtime glue (Assembly-CSharp in Unity).
$M -target:library -nowarn:0414,0169,0649,0162,0429 -out:"$OUT/jobs-glue.dll" $REFS -r:"$OUT/NaijaLife.Jobs.Core.dll" \
  "$OUT/stubs.cs" "$J"/*.cs "$J"/Conductor/*.cs "$ROOT"/Assets/Scripts/Economy/*.cs "$ROOT/Assets/Scripts/Player/PlayerNeeds.cs"

# 3. Editor scene builder. The reference UnityEditor is 2018.1; errors for
# APIs newer than that (ALLOW) are expected, anything else fails.
ALLOW='SaveAsPrefabAsset'
set +e
LOG=$($M -target:library -nowarn:0414,0169,0649 -out:"$OUT/jobs-editor.dll" $REFS -r:ed/lib/UnityEditor.dll \
  -r:"$OUT/NaijaLife.Jobs.Core.dll" -r:"$OUT/jobs-glue.dll" "$ROOT"/Assets/Editor/Jobs/*.cs 2>&1)
set -e
BAD=$(printf '%s\n' "$LOG" | grep 'error CS' | grep -vE "$ALLOW" || true)
if [ -n "$BAD" ]; then printf '%s\n' "$BAD"; echo "FAIL: editor code"; exit 1; fi
printf '%s\n' "$LOG" | grep -q 'error CS' && echo "note: only newer-than-2018.1 editor APIs missing ($ALLOW), as expected"
echo "PASS: Jobs.Core tests pass; jobs runtime glue and scene builder compile against the reference assemblies."
