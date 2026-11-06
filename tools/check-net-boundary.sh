#!/usr/bin/env bash
# Netcode boundary guard (docs/specs/netcode-m5.md §1): only
# Assets/Scripts/Network/Fusion/ may use the Fusion SDK. Run before every push
# that touches Assets/. Exit 1 = a gameplay file talks to Fusion directly.
cd "$(dirname "$0")/.." || exit 2
bad=$(grep -rlE '^\s*using\s+Fusion(\.|;)|\bNetworkRunner\b|\bNetworkBehaviour\b' Assets --include='*.cs' \
      | grep -v '^Assets/Scripts/Network/Fusion/' || true)
if [ -n "$bad" ]; then echo "Fusion used outside Assets/Scripts/Network/Fusion/:"; echo "$bad"; exit 1; fi
# Nothing private on the wire: these words must not appear in the code that
# builds Photon state, RPCs and chat (Core + Fusion). Network/Unity talks to
# Supabase over the player's own token (wallet, card) and is not on the wire.
leak=$(grep -rniE '\b(nin|email|dob|date_?of_?birth|wallet|balance|naira)\b' Assets/Scripts/Network/Core Assets/Scripts/Network/Fusion --include='*.cs' \
       | grep -viE '^\S+:\s*[0-9]+:\s*//' || true)
if [ -n "$leak" ]; then echo "Private-looking field in network code (check it):"; echo "$leak"; exit 1; fi
echo "net boundary OK"
