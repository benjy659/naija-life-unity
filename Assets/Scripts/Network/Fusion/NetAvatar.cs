// Naija Streets (Unity) — the invisible per-player Fusion object.
// It only carries data: packed pose, public state, events and lobby chat.
// The game draws remote players itself (RemotePlayerSpawner + SnapshotBuffer),
// so feel stays the same if the transport changes. No NetworkTransform.
// Prefab: empty GameObject + NetworkObject (Object Interest = Area Of Interest)
// + this component. Register it in the Fusion prefab table.
// SKELETON, UNVERIFIED (2026-11-05): compiled only with NL_FUSION; fix against the SDK (M5.1c).

#if NL_FUSION
using Fusion;
using UnityEngine;

namespace NaijaLife.Network.Fusion
{
    public sealed class NetAvatar : NetworkBehaviour
    {
        const float InterestExtents = 300f; // own 200 m cell + the ring around it

        [Networked] ushort Seq { get; set; }
        [Networked] float PX { get; set; }
        [Networked] float PY { get; set; }
        [Networked] float PZ { get; set; }
        [Networked] ushort Heading { get; set; }   // 0..65535 = 0..360 degrees
        [Networked] ushort SpeedCm { get; set; }
        [Networked] byte FlagsAnim { get; set; }   // low 4 bits flags, high 4 bits anim id
        [Networked] ushort Vehicle { get; set; }

        [Networked] ushort AppearanceId { get; set; }
        [Networked] ushort StateVehicle { get; set; }
        [Networked] ushort PublicFlags { get; set; }

        ushort _seenSeq;
        bool _seenAny;
        PublicState _seenState;

        FusionNetSession Session => FusionNetSession.Of(Runner);

        // ---- local (state authority) writes ----

        public void WritePose(in NetPose p, ushort seq)
        {
            PX = p.X; PY = p.Y; PZ = p.Z;
            Heading = MoveCodec.HeadingToU16(p.HeadingDeg);
            SpeedCm = (ushort)Mathf.Clamp(Mathf.RoundToInt(p.Speed * 100f), 0, ushort.MaxValue);
            FlagsAnim = (byte)(((byte)p.Flags & 0x0F) | ((p.AnimId & 0x0F) << 4));
            Vehicle = p.VehicleModelId;
            Seq = seq;
            transform.position = new Vector3(p.X, p.Y, p.Z); // Fusion AoI uses the object's position
        }

        public void WriteState(PublicState s)
        {
            AppearanceId = s.AppearanceId;
            StateVehicle = s.VehicleModelId;
            PublicFlags = s.PublicFlags;
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;
            FusionNetSession s = Session;
            if (s == null) return;
            // Check the call name in the installed SDK (Fusion 2 shared-mode AoI).
            Runner.AddPlayerAreaOfInterest(Runner.LocalPlayer,
                new Vector3(s.InterestX, 0f, s.InterestZ), InterestExtents);
        }

        // ---- remote reads ----

        public override void Render()
        {
            if (HasStateAuthority) return;
            FusionNetSession s = Session;
            if (s == null || !s.TryGetId(Object.StateAuthority, out NetPlayerId from)) return;

            if (!_seenAny || Seq != _seenSeq)
            {
                _seenAny = true;
                _seenSeq = Seq;
                var pose = new NetPose
                {
                    X = PX, Y = PY, Z = PZ,
                    HeadingDeg = Heading * (360f / 65536f),
                    Speed = SpeedCm / 100f,
                    Flags = (MoveFlags)(FlagsAnim & 0x0F),
                    AnimId = (byte)(FlagsAnim >> 4),
                    VehicleModelId = Vehicle,
                };
                s.PlayersCore.OnRemotePose(from, pose, Seq, s.Now);
            }

            var st = new PublicState { AppearanceId = AppearanceId, VehicleModelId = StateVehicle, PublicFlags = PublicFlags };
            if (!st.Equals(_seenState)) { _seenState = st; s.PlayersCore.OnRemoteState(from, st); }
        }

        // ---- events and lobby chat ----

        [Rpc(RpcSources.StateAuthority, RpcTargets.Proxies)]
        public void RpcEvent(byte kind, ushort arg, RpcInfo info = default)
        {
            FusionNetSession s = Session;
            if (s == null || info.Source != Object.StateAuthority) return; // only the owner may speak for this avatar
            if (s.TryGetId(info.Source, out NetPlayerId from)) s.EventsCore.OnRemote(from, kind, arg);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RpcLobby(string text, RpcInfo info = default)
        {
            FusionNetSession s = Session;
            if (s == null || text == null || text.Length > NetLimits.ChatMaxChars) return;
            if (info.Source != Object.StateAuthority) return;
            if (s.TryGetId(info.Source, out NetPlayerId from)) s.ChatCore.OnLobby(from, text);
        }
    }
}
#endif
