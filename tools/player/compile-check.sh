#!/usr/bin/env bash
# Compile check for the M1 player C# without Unity (cloud threads have no
# Editor). Same method as tools/vehicles/compile-check.sh:
#   Assets/Scripts/Player (PlayerInput, PlayerController, PlayerCamera,
#   IPlayerCameraTarget) and Assets/Scripts/Verification/M1VerifyHarness.cs
#   against UnityEngine 2021.3 module reference assemblies, with stubs for the
#   Input System 1.x and Cinemachine 3.1 shaped like the real APIs
#   (stubs-runtime.cs.txt). PlayerNeeds.cs belongs to the jobs check.
# It catches typos, missing usings and wrong types. It does NOT prove the code
# compiles in Unity 6 or that the camera feels right: Ted's harness run
# (inbox IN-13, IN-15) is still the check.
#
#   apt-get install -y mono-mcs mono-runtime   (once)
#   bash tools/player/compile-check.sh
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
CACHE="${UNITY_REF_CACHE:-/tmp/unity-ref-cache}"
OUT="$CACHE/player"
mkdir -p "$OUT"; cd "$CACHE"
get() { [ -d "$2" ] || { curl -sSL -o "$2.nupkg" "$1" && unzip -q -o "$2.nupkg" -d "$2"; }; }
get https://api.nuget.org/v3-flatcontainer/unityengine.modules/2021.3.33/unityengine.modules.2021.3.33.nupkg ue
cp "$ROOT/tools/player/stubs-runtime.cs.txt" "$OUT/stubs.cs"
REFS=$(ls ue/lib/net45/*.dll | sed 's/^/-r:/' | tr '\n' ' ')
P="$ROOT/Assets/Scripts/Player"
mcs -nologo -langversion:experimental -target:library -nowarn:0414,0169,0649 -out:"$OUT/player.dll" $REFS \
  "$OUT/stubs.cs" "$P/IPlayerCameraTarget.cs" "$P/PlayerInput.cs" "$P/PlayerController.cs" "$P/PlayerCamera.cs" \
  "$ROOT/Assets/Scripts/Verification/M1VerifyHarness.cs"
echo "PASS: M1 player and harness code compile against the reference assemblies."
