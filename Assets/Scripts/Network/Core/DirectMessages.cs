// Naija Streets (Unity) — M5.3 pure helpers for DMs and friends (no Unity, no SDK).
// DMs go over Supabase Realtime private broadcast, never over Fusion.
// The live RLS check nl_rt_dm_ok (checked 2026-11-06) only lets the two players
// of a conversation read or write its topic:
//   topic  d:<lo uuid>:<hi uuid>   (lowercase, lo < hi, never yourself)
//   and both are signed-in, non-anonymous, have a claimed username, no block
//   either way, neither actively banned.
// So the sender of a DM is always "the other side of the topic". The payload
// carries no sender id, and the receiver never trusts one.
// Spec: docs/specs/netcode-m5.md §8 (M5.3).

using System;

namespace NaijaLife.Network
{
    public static class DmTopic
    {
        public const string Event = "dm";

        /// <summary>True for a canonical lowercase uuid (8-4-4-4-12 hex), the only form the server accepts.</summary>
        public static bool IsUuid(string s)
        {
            if (s == null || s.Length != 36) return false;
            for (int i = 0; i < 36; i++)
            {
                char c = s[i];
                bool dash = i == 8 || i == 13 || i == 18 || i == 23;
                if (dash) { if (c != '-') return false; }
                else if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return false;
            }
            return true;
        }

        /// <summary>The conversation topic for two players, or null when either id is not a uuid or both are the same.</summary>
        public static string For(NetPlayerId me, NetPlayerId other)
        {
            string a = Lower(me.Value), b = Lower(other.Value);
            if (!IsUuid(a) || !IsUuid(b)) return null;
            int c = string.CompareOrdinal(a, b);
            if (c == 0) return null;
            return c < 0 ? "d:" + a + ":" + b : "d:" + b + ":" + a;
        }

        /// <summary>The other player of a topic I am part of. False for a bad topic or one that is not mine.</summary>
        public static bool TryOther(string topic, NetPlayerId me, out NetPlayerId other)
        {
            other = default;
            if (topic == null || topic.Length != 2 + 36 + 1 + 36 || !topic.StartsWith("d:", StringComparison.Ordinal) || topic[38] != ':')
                return false;
            string lo = topic.Substring(2, 36), hi = topic.Substring(39, 36);
            if (!IsUuid(lo) || !IsUuid(hi) || string.CompareOrdinal(lo, hi) >= 0) return false;
            string m = Lower(me.Value);
            if (m == lo) { other = new NetPlayerId(hi); return true; }
            if (m == hi) { other = new NetPlayerId(lo); return true; }
            return false;
        }

        static string Lower(string s) => string.IsNullOrEmpty(s) ? s : s.ToLowerInvariant();
    }

    /// <summary>Why a friends call failed. Codes come from the live RPCs' "error" field.</summary>
    public enum FriendError
    {
        None, Offline, Denied, NoSuchPlayer, Self, Blocked, Already, FriendCap, RateLimited, NoPending, Unknown,
    }

    public static class FriendErrors
    {
        public static FriendError Parse(string code)
        {
            switch (code)
            {
                case null: case "": return FriendError.None;
                case "denied": return FriendError.Denied;
                case "no_such_player": return FriendError.NoSuchPlayer;
                case "self": return FriendError.Self;
                case "blocked": return FriendError.Blocked;
                case "already": return FriendError.Already;
                case "friend_cap": return FriendError.FriendCap;
                case "rate_limited": return FriendError.RateLimited;
                case "no_pending": return FriendError.NoPending;
                default: return FriendError.Unknown;
            }
        }

        /// <summary>Short player-facing line for a toast. Never shows the raw code.</summary>
        public static string Message(FriendError e)
        {
            switch (e)
            {
                case FriendError.None: return "Done";
                case FriendError.Offline: return "No connection. Try again.";
                case FriendError.Denied: return "Sign in with a username to add friends.";
                case FriendError.NoSuchPlayer: return "No player with that name.";
                case FriendError.Self: return "That's you!";
                case FriendError.Blocked: return "You can't add this player.";
                case FriendError.Already: return "Already friends or request sent.";
                case FriendError.FriendCap: return "Your friends list is full.";
                case FriendError.RateLimited: return "Slow down. Try again later.";
                case FriendError.NoPending: return "That request is gone.";
                default: return "Something went wrong.";
            }
        }
    }

    /// <summary>One accepted friend, as nl_friends_list returns it (no user id, no private fields).</summary>
    public sealed class FriendInfo
    {
        public string Username;
        public string AvatarRef;
        public string MetPlace;
        public string SinceUtc;
    }
}
