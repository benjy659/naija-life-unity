// Naija Streets (Unity) — M5.3 part 1: SEND a DM over Supabase Realtime private
// broadcast, through the REST endpoint POST /realtime/v1/api/broadcast (plain
// HTTP, so it works in WebGL without a socket). The live RLS policy
// nl_rt_dm_write (nl_rt_dm_ok) decides who may write a topic: only the two
// players, both non-anonymous with a claimed username, no block, no ban.
// M5.1's anonymous sign-in therefore cannot DM; that is by design.
//
// Payload is {text, ts} only. No sender id: the receiver takes the sender from
// the topic (DmTopic.TryOther), so it cannot be faked. Nothing private
// (Game NIN, email, DOB, money) ever goes in a DM payload.
//
// Part 2 (next): RECEIVE over the Realtime WebSocket (WebGL .jslib bridge +
// ClientWebSocket in the Editor), joining d:<me>:<other> topics and calling
// ChatCore.OnDirect(other, text).
// UNVERIFIED (2026-11-06): not compiled in Unity or run against the server yet.

using System;
using System.Threading;
using System.Threading.Tasks;
using NaijaLife.Network;
using UnityEngine;

namespace NaijaLife.NetworkGlue
{
    public sealed class SupabaseDirectMessages
    {
        [Serializable] class Payload { public string text; public long ts; }
        [Serializable] class Message { public string topic; public string @event; public Payload payload; public bool @private; }
        [Serializable] class Batch { public Message[] messages; }

        readonly SupabaseSession _sb;

        public SupabaseDirectMessages(SupabaseSession sb) { _sb = sb ?? throw new ArgumentNullException(nameof(sb)); }

        /// <summary>
        /// Matches ChatCore's sendDirect: the line is already normalized, checked and rate
        /// limited. False when not signed in, the topic is bad, or the server refuses.
        /// </summary>
        public async Task<bool> SendAsync(NetPlayerId to, string line, CancellationToken cancel)
        {
            if (string.IsNullOrEmpty(line)) return false;
            string token = await _sb.GetAccessTokenAsync(); // also fills UserId
            if (token == null || cancel.IsCancellationRequested) return false;
            string topic = DmTopic.For(new NetPlayerId(_sb.UserId), to);
            if (topic == null) return false;

            string json = JsonUtility.ToJson(new Batch
            {
                messages = new[]
                {
                    new Message
                    {
                        topic = topic,
                        @event = DmTopic.Event,
                        payload = new Payload { text = line, ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
                        @private = true, // RLS applies only to private channels
                    },
                },
            });
            var r = await _sb.PostJsonAsync("/realtime/v1/api/broadcast", json);
            return r.ok && !cancel.IsCancellationRequested; // 202 Accepted
        }
    }
}
