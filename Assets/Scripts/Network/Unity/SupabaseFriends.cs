// Naija Streets (Unity) — M5.3 friends over the LIVE Supabase RPCs (checked 2026-11-06):
//   nl_friends_list()                                  -> {ok, friends:[{username, avatar_ref, met_place, since}]}
//   nl_friend_request(p_username, p_met_place, p_request_id) -> {ok} | {ok:false, error}
//   nl_friend_respond(p_username, p_accept, p_request_id)    -> {ok, accepted}
//   nl_friend_remove(p_username, p_request_id)               -> {ok}
// The server does every check (bans, blocks, caps, rate limits) and audits each
// call; the client only shows the answer. Anonymous players get "denied".
// Spec: docs/specs/netcode-m5.md §8.
// UNVERIFIED (2026-11-06): not compiled in Unity or run against the server yet.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NaijaLife.Network;
using UnityEngine;

namespace NaijaLife.NetworkGlue
{
    public sealed class SupabaseFriends
    {
        [Serializable] class Reply { public bool ok; public string error; }
        [Serializable] class ListReply { public bool ok; public string error; public FriendRow[] friends; }
        [Serializable] class FriendRow { public string username; public string avatar_ref; public string met_place; public string since; }
        [Serializable] class RequestArgs { public string p_username; public string p_met_place; public string p_request_id; }
        [Serializable] class RespondArgs { public string p_username; public bool p_accept; public string p_request_id; }
        [Serializable] class RemoveArgs { public string p_username; public string p_request_id; }

        readonly SupabaseSession _sb;

        public SupabaseFriends(SupabaseSession sb) { _sb = sb ?? throw new ArgumentNullException(nameof(sb)); }

        public async Task<(FriendError error, IReadOnlyList<FriendInfo> friends)> ListAsync()
        {
            var r = await _sb.RpcAsync("nl_friends_list", "{}");
            if (!r.ok) return (r.status == 0 ? FriendError.Offline : FriendError.Unknown, Array.Empty<FriendInfo>());
            ListReply reply = Parse<ListReply>(r.body);
            if (reply == null) return (FriendError.Unknown, Array.Empty<FriendInfo>());
            if (!reply.ok) return (Err(reply.error), Array.Empty<FriendInfo>());
            var list = new List<FriendInfo>();
            if (reply.friends != null)
                foreach (FriendRow f in reply.friends)
                    if (f != null && !string.IsNullOrEmpty(f.username))
                        list.Add(new FriendInfo { Username = f.username, AvatarRef = f.avatar_ref, MetPlace = f.met_place, SinceUtc = f.since });
            return (FriendError.None, list);
        }

        /// <param name="metPlace">Where you met, e.g. "Oshodi". Optional, shown on the friend card.</param>
        public Task<FriendError> RequestAsync(string username, string metPlace = null) =>
            Call("nl_friend_request", string.IsNullOrEmpty(metPlace)
                // JsonUtility writes a null string as "", so leave the field out to send SQL null.
                ? JsonUtility.ToJson(new RemoveArgs { p_username = username, p_request_id = NewId() })
                : JsonUtility.ToJson(new RequestArgs { p_username = username, p_met_place = metPlace, p_request_id = NewId() }));

        public Task<FriendError> RespondAsync(string username, bool accept) =>
            Call("nl_friend_respond", JsonUtility.ToJson(new RespondArgs { p_username = username, p_accept = accept, p_request_id = NewId() }));

        public Task<FriendError> RemoveAsync(string username) =>
            Call("nl_friend_remove", JsonUtility.ToJson(new RemoveArgs { p_username = username, p_request_id = NewId() }));

        async Task<FriendError> Call(string rpc, string json)
        {
            var r = await _sb.RpcAsync(rpc, json);
            if (!r.ok) return r.status == 0 ? FriendError.Offline : FriendError.Unknown;
            Reply reply = Parse<Reply>(r.body);
            if (reply == null) return FriendError.Unknown;
            return reply.ok ? FriendError.None : Err(reply.error);
        }

        static FriendError Err(string code)
        {
            FriendError e = FriendErrors.Parse(code);
            return e == FriendError.None ? FriendError.Unknown : e; // ok:false with no code is still a failure
        }

        // The id lets the server's audit tie a retry to the first try.
        static string NewId() => Guid.NewGuid().ToString("D");

        static T Parse<T>(string json) where T : class
        {
            if (string.IsNullOrEmpty(json)) return null;
            try { return JsonUtility.FromJson<T>(json); } catch (Exception) { return null; }
        }
    }
}
