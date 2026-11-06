// Naija Streets (Unity) — minimal Supabase client for M5.1: sign in, keep the
// token fresh, look up street cards. REST only (UnityWebRequest works in WebGL).
// Only the public anon key is used here; never a service key.
// UNVERIFIED (2026-11-05): not compiled or run yet.
//
// M5.1 sign-in is anonymous (Supabase anonymous sign-ins, as the browser game
// used). The real sign-up / sign-in screens are the UI workstream's job; they
// will call SetSession() with their tokens instead.

using System;
using System.Threading;
using System.Threading.Tasks;
using NaijaLife.Network;
using UnityEngine;
using UnityEngine.Networking;

namespace NaijaLife.NetworkGlue
{
    [Serializable]
    public sealed class SupabaseConfig
    {
        [Tooltip("https://<ref>.supabase.co")] public string Url = "https://jdumcxvqjtgnbeuvlufq.supabase.co";
        [Tooltip("Public anon key only (Ted's team pastes it locally; it is public by design).")] public string AnonKey = "";
    }

    public sealed class SupabaseSession
    {
        const string RefreshPref = "nl.sb.refresh";

        [Serializable] class TokenReply { public string access_token; public string refresh_token; public int expires_in; public UserReply user; }
        [Serializable] class UserReply { public string id; }
        [Serializable] class CardReq { public string p_uid; }
        // Same jsonb as live nl_street_card: ok, username, avatar_ref, member_since, crew{name,type,emblem,city,role}, office, rep_tier (null for now).
        [Serializable] class CardReply { public bool ok; public string username; public string avatar_ref; public string member_since; public CrewReply crew; }
        [Serializable] class CrewReply { public string name; public string type; public string city; public string role; }

        readonly SupabaseConfig _cfg;
        string _access, _refresh;
        double _expiresAt;

        public string UserId { get; private set; }
        public bool SignedIn => !string.IsNullOrEmpty(_access);

        public SupabaseSession(SupabaseConfig cfg) { _cfg = cfg; }

        /// <summary>Valid access token, refreshing or signing in when needed. Null on failure.</summary>
        public async Task<string> GetAccessTokenAsync()
        {
            if (SignedIn && Time.realtimeSinceStartupAsDouble < _expiresAt - 60) return _access;
            string saved = _refresh ?? PlayerPrefs.GetString(RefreshPref, "");
            if (!string.IsNullOrEmpty(saved) &&
                await TokenCall("/auth/v1/token?grant_type=refresh_token", "{\"refresh_token\":\"" + saved + "\"}"))
                return _access;
            if (await TokenCall("/auth/v1/signup", "{}")) return _access; // anonymous sign-in
            return null;
        }

        public void SetSession(string access, string refresh, int expiresIn, string userId)
        {
            _access = access; _refresh = refresh; UserId = userId;
            _expiresAt = Time.realtimeSinceStartupAsDouble + expiresIn;
            PlayerPrefs.SetString(RefreshPref, refresh ?? "");
        }

        async Task<bool> TokenCall(string path, string body)
        {
            using (var req = Post(path, body, null))
            {
                await req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success) return false;
                TokenReply r = JsonUtility.FromJson<TokenReply>(req.downloadHandler.text);
                if (r == null || string.IsNullOrEmpty(r.access_token)) return false;
                SetSession(r.access_token, r.refresh_token, r.expires_in, r.user != null ? r.user.id : null);
                return true;
            }
        }

        /// <summary>
        /// Street card by user id. Needs the new RPC nl_street_card_by_id (spec §3.5,
        /// task M5.1f). Until it is live this returns null and name tags show "Player".
        /// </summary>
        public async Task<StreetCard> FetchStreetCardAsync(NetPlayerId id, CancellationToken cancel)
        {
            string token = await GetAccessTokenAsync();
            if (token == null) return null;
            string body = JsonUtility.ToJson(new CardReq { p_uid = id.Value });
            using (var req = Post("/rest/v1/rpc/nl_street_card_by_id", body, token))
            {
                await req.SendWebRequest();
                if (cancel.IsCancellationRequested || req.result != UnityWebRequest.Result.Success) return null;
                CardReply r = JsonUtility.FromJson<CardReply>(req.downloadHandler.text);
                if (r == null || !r.ok || string.IsNullOrEmpty(r.username)) return null;
                return new StreetCard
                {
                    Username = r.username,
                    HomeCity = null, // the card has no home city yet
                    CrewName = r.crew != null && !string.IsNullOrEmpty(r.crew.name) ? r.crew.name : null,
                    CrewRole = r.crew != null && !string.IsNullOrEmpty(r.crew.role) ? r.crew.role : null,
                    RepTier = 0, // server sends null until rep tiers exist
                };
            }
        }

        /// <summary>
        /// Call a Supabase RPC as the signed-in player. Returns (ok, body). Never throws
        /// for network reasons. Body is the raw JSON reply (or null on failure).
        /// </summary>
        public async Task<(bool ok, long status, string body)> RpcAsync(string name, string jsonArgs)
        {
            string token = await GetAccessTokenAsync();
            if (token == null) return (false, 0, null);
            using (var req = Post("/rest/v1/rpc/" + name, string.IsNullOrEmpty(jsonArgs) ? "{}" : jsonArgs, token))
            {
                await req.SendWebRequest();
                bool ok = req.result == UnityWebRequest.Result.Success;
                return (ok, req.responseCode, ok ? req.downloadHandler.text : null);
            }
        }

        /// <summary>
        /// POST JSON to any Supabase path (e.g. "/realtime/v1/api/broadcast") as the signed-in
        /// player. Returns (ok, status, body). Never throws for network reasons.
        /// </summary>
        public async Task<(bool ok, long status, string body)> PostJsonAsync(string path, string json)
        {
            string token = await GetAccessTokenAsync();
            if (token == null) return (false, 0, null);
            using (var req = Post(path, string.IsNullOrEmpty(json) ? "{}" : json, token))
            {
                await req.SendWebRequest();
                bool ok = req.result == UnityWebRequest.Result.Success;
                return (ok, req.responseCode, ok ? req.downloadHandler.text : null);
            }
        }

        /// <summary>
        /// GET a PostgREST path (e.g. "/rest/v1/economy_ledger?select=...") as the signed-in
        /// player. Row-level security limits it to the player's own rows. Never throws for
        /// network reasons.
        /// </summary>
        public async Task<(bool ok, long status, string body)> GetAsync(string pathAndQuery)
        {
            string token = await GetAccessTokenAsync();
            if (token == null) return (false, 0, null);
            using (var req = UnityWebRequest.Get(_cfg.Url.TrimEnd('/') + pathAndQuery))
            {
                req.SetRequestHeader("apikey", _cfg.AnonKey);
                req.SetRequestHeader("Authorization", "Bearer " + token);
                req.timeout = 15;
                await req.SendWebRequest();
                bool ok = req.result == UnityWebRequest.Result.Success;
                return (ok, req.responseCode, ok ? req.downloadHandler.text : null);
            }
        }

        UnityWebRequest Post(string path, string json, string bearer)
        {
            var req = new UnityWebRequest(_cfg.Url.TrimEnd('/') + path, "POST");
            req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("apikey", _cfg.AnonKey);
            // The anon key is only ever the apikey; the bearer is the player's own JWT.
            if (!string.IsNullOrEmpty(bearer)) req.SetRequestHeader("Authorization", "Bearer " + bearer);
            req.timeout = 15;
            return req;
        }
    }
}
