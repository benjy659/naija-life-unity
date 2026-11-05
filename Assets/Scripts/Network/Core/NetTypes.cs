// Naija Streets (Unity) — M5 netcode layer, shared types.
// Spec: docs/specs/netcode-m5.md. Plain C#: no UnityEngine, no Fusion.
// UNVERIFIED (2026-11-05): written without a Unity Editor; not compiled yet.

using System;

namespace NaijaLife.Network
{
    /// <summary>Connection state shown on the HUD. The game stays playable in every state.</summary>
    public enum NetStatus
    {
        Offline = 0,
        Connecting = 1,
        Online = 2,
        Reconnecting = 3,
        Failed = 4,
    }

    /// <summary>Result of a join. Network problems never throw; they come back here.</summary>
    public enum NetResult
    {
        Ok = 0,
        AuthFailed = 1,
        RoomFull = 2,
        Timeout = 3,
        NoNetwork = 4,
        Cancelled = 5,
        Error = 6,
    }

    /// <summary>
    /// A player on the network = the Supabase auth.uid() as text, stamped by the
    /// server (Photon custom auth). Never chosen by the client, never from a payload.
    /// </summary>
    public readonly struct NetPlayerId : IEquatable<NetPlayerId>
    {
        public readonly string Value;
        public NetPlayerId(string value) { Value = value ?? string.Empty; }
        public bool IsValid => !string.IsNullOrEmpty(Value);
        public bool Equals(NetPlayerId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is NetPlayerId o && Equals(o);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value;
        public static bool operator ==(NetPlayerId a, NetPlayerId b) => a.Equals(b);
        public static bool operator !=(NetPlayerId a, NetPlayerId b) => !a.Equals(b);
    }

    /// <summary>One district instance, e.g. lagos:oshodi:1.</summary>
    public readonly struct RoomKey : IEquatable<RoomKey>
    {
        public readonly string City;      // "lagos" | "abuja"
        public readonly string District;  // "oshodi", "wuse", ...
        public readonly int Instance;     // 1, 2, ... (next one when full)

        public RoomKey(string city, string district, int instance)
        {
            City = (city ?? string.Empty).ToLowerInvariant();
            District = (district ?? string.Empty).ToLowerInvariant();
            Instance = instance < 1 ? 1 : instance;
        }

        public RoomKey Next() => new RoomKey(City, District, Instance + 1);
        public override string ToString() => City + ":" + District + ":" + Instance;
        public bool Equals(RoomKey o) => City == o.City && District == o.District && Instance == o.Instance;
        public override bool Equals(object obj) => obj is RoomKey o && Equals(o);
        public override int GetHashCode() => ToString().GetHashCode();
    }

    [Flags]
    public enum MoveFlags : byte
    {
        None = 0,
        Moving = 1 << 0,
        Driving = 1 << 1,
        Running = 1 << 2,
        Indoors = 1 << 3,
        // bits 4-7 = animation id (0-15), see NetPose.AnimId
    }

    /// <summary>
    /// What gameplay hands the replicator each frame, and what it gets back for a
    /// remote player. World metres, heading in degrees (0 = +Z, clockwise, like
    /// Unity's transform.eulerAngles.y).
    /// </summary>
    public struct NetPose
    {
        public float X, Y, Z;
        public float HeadingDeg;
        public float Speed;          // horizontal m/s
        public MoveFlags Flags;      // low 4 bits only
        public byte AnimId;          // 0-15
        public ushort VehicleModelId; // 0 = on foot

        public bool IsMoving => (Flags & MoveFlags.Moving) != 0;
        public bool IsDriving => (Flags & MoveFlags.Driving) != 0;
        public bool IsIndoors => (Flags & MoveFlags.Indoors) != 0;
    }

    /// <summary>
    /// Slow public state (≤ 1 change per 5 s). Fixed fields on purpose: there is no
    /// place to put a name, NIN, email, date of birth, money or home location.
    /// Adding a field needs a spec change (docs/specs/netcode-m5.md §5).
    /// </summary>
    public struct PublicState : IEquatable<PublicState>
    {
        public ushort AppearanceId;
        public ushort VehicleModelId;
        public ushort PublicFlags;   // bit0 away, bit1 in a job shift, others reserved

        public bool Equals(PublicState o) =>
            AppearanceId == o.AppearanceId && VehicleModelId == o.VehicleModelId && PublicFlags == o.PublicFlags;
        public override bool Equals(object obj) => obj is PublicState o && Equals(o);
        public override int GetHashCode() => (AppearanceId << 16) ^ (VehicleModelId << 4) ^ PublicFlags;
    }

    public enum WorldEventKind : byte
    {
        Wave = 1,
        Hail = 2,
        Horn = 3,
        Emote = 4,         // Arg = emote id
        EnterVehicle = 5,  // Arg = vehicle model id
        ExitVehicle = 6,   // Arg = vehicle model id
    }

    public readonly struct WorldEvent
    {
        public readonly WorldEventKind Kind;
        public readonly ushort Arg;
        public WorldEvent(WorldEventKind kind, ushort arg) { Kind = kind; Arg = arg; }
        public static bool IsKnown(byte kind) => kind >= 1 && kind <= 6;
    }

    /// <summary>What a client needs to join one district room.</summary>
    public sealed class NetJoinRequest
    {
        /// <summary>Supabase access token (JWT). Sent to Photon custom auth only. Never logged.</summary>
        public string AccessToken;
        public RoomKey Room;
        public int MaxPlayers = NetLimits.MaxPlayersPerRoom;
        public TimeSpan Timeout = TimeSpan.FromSeconds(15);
    }

    /// <summary>All numbers from the spec in one place.</summary>
    public static class NetLimits
    {
        public const int MaxPlayersPerRoom = 80;
        public const int MaxNearby = 40;

        public const double DriveSendInterval = 0.25;   // 4 Hz
        public const double WalkSendInterval = 0.5;     // 2 Hz (B-34)
        public const double KeepAliveInterval = 5.0;
        public const double PublicStateMinInterval = 5.0;
        public const float StillPosEpsilon = 0.05f;     // 5 cm
        public const float StillHeadingEpsilonDeg = 1.0f;

        public const double WalkerRenderDelay = 0.6;
        public const double DriverRenderDelay = 0.3;
        public const double DelaySlidePerSecond = 0.3;
        public const double MaxExtrapolation = 0.25;
        public const float MaxFootSpeed = 9f;
        public const float MaxDriveSpeed = 45f;
        public const float SnapDistance = 30f;
        public const int BadSnapshotsToHide = 5;
        public const double BadSnapshotWindow = 10.0;

        public const float CellSize = 200f;
        public const double CellLeaveDelay = 3.0;

        public const int EventsPerSecond = 2;
        public const int ChatMaxChars = 200;
        public const int ChatPerSecond = 1;

        public const double StreetCardCacheSeconds = 600.0; // 10 min
        public const double BackoffBaseSeconds = 1.0;
        public const double BackoffMaxSeconds = 30.0;
        public const double BackoffResetAfterOnline = 60.0;
    }
}
