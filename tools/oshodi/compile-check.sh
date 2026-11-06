#!/usr/bin/env bash
# Compile check for the Oshodi C# without Unity (cloud threads have no Editor).
# Builds Assets/Scripts/World, Assets/Editor/World and Assets/Tests/EditMode/World
# with mono's mcs against UnityEngine 2021.3 module reference assemblies and the
# UnityEditor 2018.1 reference assembly from NuGet, plus small Addressables stubs
# (the .cs.txt files next to this script; .txt so Unity never compiles them).
# It catches typos, missing usings and wrong types. It does NOT prove the code
# compiles in Unity 6 (newer APIs, other C# rules) or that it works: Ted's Editor
# run is still the check. Note: mcs rejects `foreach (var (a, b) in ...)`,
# which Unity accepts; write it without deconstruction so this check can run.
#
#   apt-get install -y mono-mcs   (once)
#   bash tools/oshodi/compile-check.sh
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
CACHE="${UNITY_REF_CACHE:-/tmp/unity-ref-cache}"
mkdir -p "$CACHE"; cd "$CACHE"
get() { [ -d "$2" ] || { curl -sSL -o "$2.nupkg" "$1" && unzip -q -o "$2.nupkg" -d "$2"; }; }
get https://api.nuget.org/v3-flatcontainer/unityengine.modules/2021.3.33/unityengine.modules.2021.3.33.nupkg ue
get https://api.nuget.org/v3-flatcontainer/unity3d.unityeditor/2018.1.6-f1/unity3d.unityeditor.2018.1.6-f1.nupkg ed
get https://api.nuget.org/v3-flatcontainer/nunit/3.5.0/nunit.3.5.0.nupkg nunit
cp "$ROOT/tools/oshodi/stubs-addressables-runtime.cs.txt" stub_rt.cs
cp "$ROOT/tools/oshodi/stubs-addressables-editor.cs.txt" stub_ed.cs
REFS=$(ls ue/lib/net45/*.dll | sed 's/^/-r:/' | tr '\n' ' ')
M="mcs -nologo -target:library -langversion:latest $REFS"
$M -out:world.dll stub_rt.cs "$ROOT"/Assets/Scripts/World/*.cs
$M -out:worlded.dll -r:ed/lib/UnityEditor.dll -r:world.dll stub_ed.cs "$ROOT"/Assets/Editor/World/*.cs
$M -out:worldtests.dll -r:ed/lib/UnityEditor.dll -r:world.dll -r:worlded.dll -r:nunit/lib/net45/nunit.framework.dll "$ROOT"/Assets/Tests/EditMode/World/*.cs
echo "PASS: Oshodi runtime, editor and test code compile against the reference assemblies."
