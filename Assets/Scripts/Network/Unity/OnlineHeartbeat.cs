// Naija Streets (Unity) — M5.4 "players online" count across all districts.
// Same server contract as the browser game (live migration 0028):
//   nl_online_touch(p_session uuid) -> int   touch this device's row, get the
//                                            number of distinct players online
//   nl_heartbeat_leave(p_session uuid)       delete the row when we hide/quit
// Rows are keyed by auth.uid() on the server; the session id is per device run.
// Every 30 s while the game is in front; the HUD shows Count with LastUpdated
// (UX rule: show when the number was last refreshed). Real counts only: when
// unknown, Count is -1 and the HUD must show nothing, never a made-up number.
// UNVERIFIED (2026-11-06): not compiled or run yet.

using System;
using System.Globalization;
using NaijaLife.Network;
using UnityEngine;

namespace NaijaLife.NetworkGlue
{
    public sealed class OnlineHeartbeat : MonoBehaviour
    {
        const float IntervalSeconds = 30f;

        public static int Count { get; private set; } = -1;
        public static DateTime LastUpdatedUtc { get; private set; }
        public static event Action<int> CountChanged;

        SupabaseSession _sb;
        string _sessionId;
        float _next;
        bool _inFront = true;
        bool _busy;
        bool _touched;

        /// <summary>NetBootstrap hands over its Supabase session.</summary>
        public void Init(SupabaseSession sb)
        {
            _sb = sb;
            _sessionId = Guid.NewGuid().ToString();
            _next = 0f; // touch at once
        }

        async void Update()
        {
            if (_sb == null || !_inFront || _busy || Time.unscaledTime < _next) return;
            _busy = true;
            _next = Time.unscaledTime + IntervalSeconds;
            try
            {
                var r = await _sb.RpcAsync("nl_online_touch", "{\"p_session\":\"" + _sessionId + "\"}");
                if (r.ok && int.TryParse(r.body.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) && n >= 0)
                {
                    _touched = true;
                    LastUpdatedUtc = DateTime.UtcNow;
                    // The local player is online too, so never show fewer than 1.
                    int shown = Math.Max(1, n);
                    if (shown != Count) { Count = shown; CountChanged?.Invoke(shown); }
                }
            }
            finally { _busy = false; }
        }

        void OnApplicationPause(bool paused) => SetInFront(!paused);
        void OnApplicationFocus(bool focus)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            SetInFront(focus); // hidden browser tab = leave, like the browser game
#endif
        }

        void SetInFront(bool inFront)
        {
            if (inFront == _inFront) return;
            _inFront = inFront;
            if (inFront) _next = 0f;   // touch at once when back
            else Leave();
        }

        void OnDestroy() => Leave();

        async void Leave()
        {
            if (_sb == null || !_touched) return;
            _touched = false;
            await _sb.RpcAsync("nl_heartbeat_leave", "{\"p_session\":\"" + _sessionId + "\"}");
        }
    }
}
