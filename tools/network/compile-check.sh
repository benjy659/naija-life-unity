#!/usr/bin/env bash
# Compile check and Core test run for the M5 netcode without Unity (cloud
# threads have no Editor). Same method as tools/jobs/compile-check.sh:
#   1. NaijaLife.Network.Core (pure C#) + its EditMode tests, compiled against
#      real NUnit 3.5 and RUN with NUnitLite.
#   2. Assets/Scripts/Network/Unity glue (+ the real UI/NLWallet.cs) against
#      UnityEngine 2021.3 module reference assemblies, with stubs for Unity 6
#      awaits, TextMeshPro and PlayerController (stubs-runtime.cs.txt).
#   3. Assets/Editor/Network against the UnityEditor 2018.1 reference assembly.
# The Fusion adapter (NL_FUSION) is NOT compiled here: no SDK. Ted's M5.1c run
# is its check. This does not prove Unity 6 compiles it; Ted's Editor run does.
#
#   apt-get install -y mono-mcs mono-runtime   (once)
#   bash tools/network/compile-check.sh
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
CACHE="${UNITY_REF_CACHE:-/tmp/unity-ref-cache}"
OUT="$CACHE/network"
mkdir -p "$OUT"; cd "$CACHE"
get() { [ -d "$2" ] || { curl -sSL -o "$2.nupkg" "$1" && unzip -q -o "$2.nupkg" -d "$2"; }; }
get https://api.nuget.org/v3-flatcontainer/unityengine.modules/2021.3.33/unityengine.modules.2021.3.33.nupkg ue
get https://api.nuget.org/v3-flatcontainer/unity3d.unityeditor/2018.1.6-f1/unity3d.unityeditor.2018.1.6-f1.nupkg ed
get https://api.nuget.org/v3-flatcontainer/nunit/3.5.0/nunit.3.5.0.nupkg nunit
get https://api.nuget.org/v3-flatcontainer/nunitlite/3.5.0/nunitlite.3.5.0.nupkg nunitlite
cp "$ROOT/tools/network/stubs-runtime.cs.txt" "$OUT/stubs.cs"
NUNIT=nunit/lib/net45/nunit.framework.dll
LITE=nunitlite/lib/net45/nunitlite.dll
REFS=$(ls ue/lib/net45/*.dll | sed 's/^/-r:/' | tr '\n' ' ')
M="mcs -nologo -langversion:experimental"
N="$ROOT/Assets/Scripts/Network"

# 1. Pure core + tests, run.
$M -target:library -out:"$OUT/NaijaLife.Network.Core.dll" "$N"/Core/*.cs
cat > "$OUT/run.cs" <<'CS'
public static class RunTests { public static int Main(string[] a) { return new NUnitLite.AutoRun(typeof(NaijaLife.Network.Tests.NetCoreTests).Assembly).Execute(a); } }
CS
$M -out:"$OUT/net-tests.exe" -r:"$NUNIT" -r:"$LITE" -r:"$OUT/NaijaLife.Network.Core.dll" \
  "$ROOT"/Assets/Tests/EditMode/Network/*.cs "$OUT/run.cs"
cp "$NUNIT" "$LITE" "$OUT/"
(cd "$OUT" && mono net-tests.exe --noresult --labels=Off) | grep -E "Test Count|Overall result|Failed :|^\s+[0-9]+\)" || true
(cd "$OUT" && mono net-tests.exe --noresult --labels=Off >/dev/null)

# 2. Runtime glue (Assembly-CSharp in Unity), with the real NLWallet.
$M -target:library -nowarn:0414,0169,0649,0162 -out:"$OUT/net-glue.dll" $REFS -r:"$OUT/NaijaLife.Network.Core.dll" \
  "$OUT/stubs.cs" "$N"/Unity/*.cs "$ROOT/Assets/Scripts/UI/NLWallet.cs"

# 3. Editor scene builder. Reference UnityEditor is 2018.1; newer APIs (ALLOW) are expected misses.
ALLOW='SaveAsPrefabAsset|FindFirstObjectByType'
set +e
LOG=$($M -target:library -nowarn:0414,0169,0649 -out:"$OUT/net-editor.dll" $REFS -r:ed/lib/UnityEditor.dll \
  -r:"$OUT/NaijaLife.Network.Core.dll" -r:"$OUT/net-glue.dll" "$OUT/stubs.cs" "$ROOT"/Assets/Editor/Network/*.cs 2>&1)
set -e
BAD=$(printf '%s\n' "$LOG" | grep 'error CS' | grep -vE "$ALLOW" || true)
if [ -n "$BAD" ]; then printf '%s\n' "$BAD"; echo "FAIL: editor code"; exit 1; fi
printf '%s\n' "$LOG" | grep -q 'error CS' && echo "note: only newer-than-2018.1 editor APIs missing ($ALLOW), as expected"
echo "PASS: Network.Core tests pass; network glue and scene builder compile against the reference assemblies."
