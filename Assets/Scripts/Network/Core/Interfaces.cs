// Naija Streets (Unity) — M5 netcode layer, the interfaces gameplay talks to.
// Spec: docs/specs/netcode-m5.md §3. Gameplay code must never reference Fusion;
// it only sees these interfaces, so Shared Mode -> Server Mode (or our own
// servers) is an adapter swap, not a rewrite.
// UNVERIFIED (2026-11-05): written without a Unity Editor; not compiled yet.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NaijaLife.Network
{
    /// <summary>One connection to one district room, plus its channels.</summary>
    public interface INetSession : IDisposable
    {
        NetStatus Status { get; }
        event Action<NetStatus> StatusChanged;

        /// <summary>Server-stamped id of this player (auth.uid()). Invalid until Online.</summary>
        NetPlayerId LocalPlayer { get; }

        /// <summary>The room we are in, or default when not joined.</summary>
        RoomKey CurrentRoom { get; }

        /// <summary>Join one district room. Never throws for network reasons.</summary>
        Task<NetResult> JoinAsync(NetJoinRequest request, CancellationToken cancel);

        /// <summary>Leave the room cleanly (district change, quit).</summary>
        Task LeaveAsync();

        /// <summary>Real connected players only. Bots never appear here.</summary>
        IReadOnlyCollection<NetPlayerId> RemotePlayers { get; }
        event Action<NetPlayerId> PlayerJoined;
        event Action<NetPlayerId> PlayerLeft;

        /// <summary>Where the local player is, for area of interest (200 m cells).</summary>
        void SetInterestCenter(float x, float z);

        /// <summary>Stop sending while the app is hidden or paused.</summary>
        void SetPaused(bool paused);

        IPlayerReplicator Players { get; }
        IWorldEvents Events { get; }
        IChatService Chat { get; }
    }

    /// <summary>Movement and slow public state of players.</summary>
    public interface IPlayerReplicator
    {
        /// <summary>Call every frame with the local pose. The replicator decides when to send.</summary>
        void SubmitLocal(NetPose pose, double now);

        /// <summary>Slow state; at most one change per 5 s goes out, the latest value wins.</summary>
        void SetLocalPublicState(PublicState state);

        /// <summary>Interpolated pose to draw now. False = do not draw this player.</summary>
        bool TryGetRemotePose(NetPlayerId id, double now, out NetPose pose);

        bool TryGetPublicState(NetPlayerId id, out PublicState state);

        /// <summary>Fills <paramref name="into"/> with at most 40 nearest remote players, nearest first.</summary>
        void GetNearby(float centerX, float centerZ, List<NetPlayerId> into);
    }

    /// <summary>Small one-off actions other players should see (wave, horn, ...).</summary>
    public interface IWorldEvents
    {
        /// <summary>False when rate limited (2 per second) or offline; nothing is sent then.</summary>
        bool Raise(WorldEventKind kind, ushort arg);

        /// <summary>Sender id comes from the transport, never from the payload.</summary>
        event Action<NetPlayerId, WorldEvent> Received;
    }

    public enum ChatRefusal
    {
        None = 0,
        Empty = 1,
        TooLong = 2,
        TooFast = 3,
        Blocked = 4,
        Offline = 5,
    }

    /// <summary>Text chat. Lobby rides the district room; DMs go over Supabase (spec §3.4).</summary>
    public interface IChatService
    {
        ChatRefusal SendLobby(string text);
        event Action<NetPlayerId, string> LobbyReceived;

        Task<ChatRefusal> SendDirectAsync(NetPlayerId to, string text, CancellationToken cancel);
        event Action<NetPlayerId, string> DirectReceived;
    }

    /// <summary>Public identity, from the Supabase nl_street_card RPC only.</summary>
    public sealed class StreetCard
    {
        public NetPlayerId Id;
        public string Username;
        public string HomeCity;
        public string CrewName;    // null when none
        public string CrewRole;    // null when none
        public int RepTier;
    }

    public interface IStreetCardService
    {
        /// <summary>Cached for 10 minutes. Null when the player has no card or the call failed.</summary>
        Task<StreetCard> GetAsync(NetPlayerId id, CancellationToken cancel);
    }
}
