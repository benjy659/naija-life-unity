// Naija Streets (Unity) — M5 movement packing, 20 bytes, little-endian.
// Spec: docs/specs/netcode-m5.md §4.5. Mirrored in tools/netsim.js for checks.
// UNVERIFIED (2026-11-05): written without a Unity Editor; not compiled yet.

using System;

namespace NaijaLife.Network
{
    public static class MoveCodec
    {
        public const byte Version = 1;
        public const int Size = 20;

        public static void Encode(in NetPose p, ushort sequence, byte[] buf, int offset = 0)
        {
            if (buf == null || buf.Length - offset < Size) throw new ArgumentException("buffer too small");
            byte flags = (byte)(((byte)p.Flags & 0x0F) | ((p.AnimId & 0x0F) << 4));
            buf[offset + 0] = Version;
            buf[offset + 1] = flags;
            PutU16(buf, offset + 2, sequence);
            PutF32(buf, offset + 4, p.X);
            PutF32(buf, offset + 8, p.Z);
            PutU16(buf, offset + 12, (ushort)(short)ClampToInt(Round(p.Y * 100f), short.MinValue, short.MaxValue));
            PutU16(buf, offset + 14, HeadingToU16(p.HeadingDeg));
            PutU16(buf, offset + 16, (ushort)ClampToInt(Round(p.Speed * 100f), 0, ushort.MaxValue));
            PutU16(buf, offset + 18, p.VehicleModelId);
        }

        /// <summary>False = drop it (wrong version or length, NaN/infinite numbers).</summary>
        public static bool TryDecode(byte[] buf, int offset, int length, out NetPose p, out ushort sequence)
        {
            p = default;
            sequence = 0;
            if (buf == null || length != Size || buf.Length - offset < Size) return false;
            if (buf[offset] != Version) return false;
            byte flags = buf[offset + 1];
            sequence = GetU16(buf, offset + 2);
            float x = GetF32(buf, offset + 4);
            float z = GetF32(buf, offset + 8);
            if (float.IsNaN(x) || float.IsInfinity(x) || float.IsNaN(z) || float.IsInfinity(z)) return false;
            p.X = x;
            p.Z = z;
            p.Y = (short)GetU16(buf, offset + 12) / 100f;
            p.HeadingDeg = GetU16(buf, offset + 14) * (360f / 65536f);
            p.Speed = GetU16(buf, offset + 16) / 100f;
            p.Flags = (MoveFlags)(flags & 0x0F);
            p.AnimId = (byte)(flags >> 4);
            p.VehicleModelId = GetU16(buf, offset + 18);
            return true;
        }

        /// <summary>True when <paramref name="seq"/> is newer than <paramref name="last"/>, wrap aware.</summary>
        public static bool IsNewer(ushort seq, ushort last) => (short)(seq - last) > 0;

        public static ushort HeadingToU16(float deg)
        {
            if (float.IsNaN(deg) || float.IsInfinity(deg)) deg = 0f;
            double d = deg % 360.0;
            if (d < 0) d += 360.0;
            return (ushort)((int)Math.Round(d / 360.0 * 65536.0) & 0xFFFF);
        }

        static int Round(float v) => (float.IsNaN(v) || float.IsInfinity(v)) ? 0 : (int)Math.Round((double)Math.Max(-2e9f, Math.Min(2e9f, v)));
        static int ClampToInt(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);

        static void PutU16(byte[] b, int o, ushort v) { b[o] = (byte)v; b[o + 1] = (byte)(v >> 8); }
        static ushort GetU16(byte[] b, int o) => (ushort)(b[o] | (b[o + 1] << 8));

        static void PutF32(byte[] b, int o, float v)
        {
            int i = BitConverter.SingleToInt32Bits(v);
            b[o] = (byte)i; b[o + 1] = (byte)(i >> 8); b[o + 2] = (byte)(i >> 16); b[o + 3] = (byte)(i >> 24);
        }

        static float GetF32(byte[] b, int o)
        {
            int i = b[o] | (b[o + 1] << 8) | (b[o + 2] << 16) | (b[o + 3] << 24);
            return BitConverter.Int32BitsToSingle(i);
        }
    }
}
