// Naija Streets (Unity) — area of interest and the 40-nearest rule.
// Spec: docs/specs/netcode-m5.md §4.3. 200 m cells, own cell + 8 around it,
// old cells kept 3 s after leaving so walking along a border does not churn.
// UNVERIFIED (2026-11-05): not compiled yet.

using System;
using System.Collections.Generic;

namespace NaijaLife.Network
{
    public readonly struct Cell : IEquatable<Cell>
    {
        public readonly int X, Z;
        public Cell(int x, int z) { X = x; Z = z; }
        public static Cell Of(float x, float z) =>
            new Cell((int)Math.Floor(x / NetLimits.CellSize), (int)Math.Floor(z / NetLimits.CellSize));
        public bool Equals(Cell o) => X == o.X && Z == o.Z;
        public override bool Equals(object obj) => obj is Cell o && Equals(o);
        public override int GetHashCode() => (X * 73856093) ^ (Z * 19349663);
        public override string ToString() => X + ":" + Z;
    }

    public sealed class CellInterest
    {
        // cell -> time it stops counting (PositiveInfinity while in the ring)
        readonly Dictionary<Cell, double> _cells = new Dictionary<Cell, double>();
        readonly List<Cell> _scratch = new List<Cell>();
        bool _has;
        Cell _center;

        public Cell Center => _center;
        public int Count => _cells.Count;
        public IEnumerable<Cell> Cells => _cells.Keys;
        public bool Contains(Cell c) => _cells.ContainsKey(c);
        public bool Contains(float x, float z) => _cells.ContainsKey(Cell.Of(x, z));

        /// <summary>Call when the local player moves. True when the set of cells changed.</summary>
        public bool Update(float x, float z, double now)
        {
            bool changed = false;
            Cell c = Cell.Of(x, z);
            if (!_has || !c.Equals(_center))
            {
                // Old ring starts its 3 s countdown; the new ring is in at once.
                _scratch.Clear();
                _scratch.AddRange(_cells.Keys);
                foreach (Cell k in _scratch)
                    if (double.IsPositiveInfinity(_cells[k]) && !InRing(k, c))
                        _cells[k] = now + NetLimits.CellLeaveDelay;
                for (int dx = -1; dx <= 1; dx++)
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        Cell n = new Cell(c.X + dx, c.Z + dz);
                        if (!_cells.ContainsKey(n)) changed = true;
                        _cells[n] = double.PositiveInfinity;
                    }
                _center = c;
                _has = true;
            }

            _scratch.Clear();
            foreach (KeyValuePair<Cell, double> kv in _cells)
                if (kv.Value <= now) _scratch.Add(kv.Key);
            foreach (Cell k in _scratch) { _cells.Remove(k); changed = true; }
            return changed;
        }

        static bool InRing(Cell k, Cell c) => Math.Abs(k.X - c.X) <= 1 && Math.Abs(k.Z - c.Z) <= 1;
    }

    public static class NearbyPicker
    {
        struct Entry { public NetPlayerId Id; public float D2; }
        [ThreadStatic] static List<Entry> _tmp;

        /// <summary>Nearest first, at most <paramref name="max"/> (spec: 40).</summary>
        public static void Pick(IEnumerable<KeyValuePair<NetPlayerId, NetPose>> players,
                                float cx, float cz, int max, List<NetPlayerId> into)
        {
            into.Clear();
            if (_tmp == null) _tmp = new List<Entry>(64);
            _tmp.Clear();
            foreach (KeyValuePair<NetPlayerId, NetPose> kv in players)
            {
                float dx = kv.Value.X - cx, dz = kv.Value.Z - cz;
                _tmp.Add(new Entry { Id = kv.Key, D2 = dx * dx + dz * dz });
            }
            _tmp.Sort((a, b) => a.D2.CompareTo(b.D2));
            int n = Math.Min(max, _tmp.Count);
            for (int i = 0; i < n; i++) into.Add(_tmp[i].Id);
        }
    }
}
