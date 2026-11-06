// Naija Streets (Unity) — creates the network session for a district scene.
// Uses Fusion when the NL_FUSION define is on, otherwise the offline session.
// Gameplay reads NetBootstrap.Session (an INetSession) and nothing else.
// UNVERIFIED (2026-11-05): not compiled or run yet. Scene setup: M5.1d.

using System.Collections.Generic;
using System.Threading;
using NaijaLife.Network;
using UnityEngine;

namespace NaijaLife.NetworkGlue
{
    public sealed class NetBootstrap : MonoBehaviour
    {
        public static INetSession Session { get; private set; }
        public static IStreetCardService StreetCards { get; private set; }

        [SerializeField] string city = "lagos";
        [SerializeField] string district = "oshodi";
        [SerializeField] SupabaseConfig supabase = new SupabaseConfig();
        [Tooltip("Lowercase words, one per line. Applied to chat on send and on receive.")]
        [SerializeField] TextAsset chatBlocklist;
        [SerializeField] bool showStatusBadge = true;

        SupabaseSession _sb;
        CancellationTokenSource _cts;

        async void Start()
        {
            _cts = new CancellationTokenSource();
            _sb = new SupabaseSession(supabase);
            StreetCards = new StreetCardCache(_sb.FetchStreetCardAsync, () => Time.realtimeSinceStartupAsDouble);
            var blocklist = LoadBlocklist();

#if NL_FUSION
            var host = GetComponent<NaijaLife.Network.Fusion.FusionSessionHost>();
            Session = host != null ? host.Create(blocklist) : new OfflineSession(() => Time.realtimeSinceStartupAsDouble);
#else
            Session = new OfflineSession(() => Time.realtimeSinceStartupAsDouble);
#endif
            string token = await _sb.GetAccessTokenAsync();
            if (token == null) { Debug.Log("[net] no sign-in; playing offline"); return; }

            // Players-online count across all districts (live nl_online_touch).
            if (!TryGetComponent(out OnlineHeartbeat hb)) hb = gameObject.AddComponent<OnlineHeartbeat>(); // not ??: Unity fake-null
            hb.Init(_sb);

            NetResult r = await Session.JoinAsync(new NetJoinRequest
            {
                AccessToken = token,
                Room = new RoomKey(city, district, 1),
            }, _cts.Token);
            Debug.Log("[net] join " + city + ":" + district + " -> " + r);
        }

        HashSet<string> LoadBlocklist()
        {
            var set = new HashSet<string>();
            if (chatBlocklist == null) return set;
            foreach (string line in chatBlocklist.text.Split('\n'))
            {
                string w = line.Trim().ToLowerInvariant();
                if (w.Length > 0 && !w.StartsWith("#")) set.Add(w);
            }
            return set;
        }

        void OnApplicationPause(bool paused) => Session?.SetPaused(paused);
        void OnApplicationFocus(bool focus)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Session?.SetPaused(!focus); // hidden browser tab: stop sending
#endif
        }

        async void OnDestroy()
        {
            _cts?.Cancel();
            INetSession s = Session;
            Session = null;
            if (s != null) { await s.LeaveAsync(); s.Dispose(); }
        }

        // Dev-only status badge until the HUD workstream adds the real one.
        void OnGUI()
        {
            if (!showStatusBadge || Session == null || Session.Status == NetStatus.Online) return;
            if (!Debug.isDebugBuild) return;
            string label = Session.Status == NetStatus.Reconnecting ? "Reconnecting..." :
                           Session.Status == NetStatus.Connecting ? "Connecting..." : "Offline";
            GUI.Label(new Rect(Screen.width - 150, 8, 140, 22), label);
        }
    }
}
