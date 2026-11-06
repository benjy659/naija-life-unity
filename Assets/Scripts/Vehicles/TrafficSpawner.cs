// M3 bot traffic pool (docs/specs/vehicles-m3.md section 8.8). Keeps up to
// maxAlive bot vehicles around the player. Spawns only out of camera view, at
// least minSpawnDistance from the player, on a lane node with nothing within
// 15 m. Despawns beyond despawnDistance when out of view, or when a bot gave up
// rerouting and is out of view more than 80 m away. Pooled, never destroyed.

using System.Collections.Generic;
using UnityEngine;

namespace NaijaLife.Vehicles
{
    [DisallowMultipleComponent]
    public sealed class TrafficSpawner : MonoBehaviour
    {
        public LaneGraph graph;
        [Tooltip("Bot prefabs. Each needs VehicleController (+ TrafficDriver is added if missing).")]
        public VehicleController[] prefabs;
        [Tooltip("Relative weights, same order as prefabs (spec: 1 in 3 danfo).")]
        public float[] weights;

        public int maxAlive = 12;
        public float minSpawnDistance = 60f;
        public float maxSpawnDistance = 200f;
        public float despawnDistance = 220f;
        public float checkInterval = 1f;

        readonly List<VehicleController> _alive = new List<VehicleController>();
        readonly List<VehicleController> _pool = new List<VehicleController>();
        readonly Collider[] _overlap = new Collider[8];
        Transform _player;
        Camera _cam;
        float _next;
        readonly Plane[] _planes = new Plane[6];

        public int AliveCount => _alive.Count;
        public IReadOnlyList<VehicleController> Alive => _alive;

        void Update()
        {
            if (Time.time < _next || graph == null || prefabs == null || prefabs.Length == 0) return;
            _next = Time.time + checkInterval;
            if (_player == null) { var go = GameObject.FindGameObjectWithTag("Player"); if (go != null) _player = go.transform; }
            if (_cam == null) _cam = Camera.main;
            if (_player == null) return;
            if (_cam != null) GeometryUtility.CalculateFrustumPlanes(_cam, _planes);

            // Despawn
            for (int i = _alive.Count - 1; i >= 0; i--)
            {
                var v = _alive[i];
                if (v == null) { _alive.RemoveAt(i); continue; }
                float d = Vector3.Distance(v.transform.position, _player.position);
                var drv = v.GetComponent<TrafficDriver>();
                bool gaveUp = drv != null && drv.WantsDespawn && d > 80f;
                if ((d > despawnDistance || gaveUp) && !Visible(v.transform.position)) Recycle(i);
            }

            // Spawn one per check, so the world fills gradually.
            if (_alive.Count < maxAlive) TrySpawn();
        }

        bool Visible(Vector3 p)
        {
            if (_cam == null) return false;
            return GeometryUtility.TestPlanesAABB(_planes, new Bounds(p + Vector3.up, new Vector3(5f, 3f, 5f)));
        }

        void TrySpawn()
        {
            graph.Refresh();
            var nodes = graph.nodes;
            if (nodes.Count == 0) return;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                var n = nodes[Random.Range(0, nodes.Count)];
                if (n == null || n.next.Count == 0 || n.crossingId != 0) continue;
                Vector3 p = n.transform.position;
                float d = Vector3.Distance(p, _player.position);
                if (d < minSpawnDistance || d > maxSpawnDistance || Visible(p)) continue;
                int hits = Physics.OverlapSphereNonAlloc(p + Vector3.up, 15f, _overlap,
                    VehicleLayers.Bit(VehicleLayers.Vehicle) | VehicleLayers.Bit(VehicleLayers.DrivenVehicle) | VehicleLayers.Bit(VehicleLayers.Pedestrian),
                    QueryTriggerInteraction.Ignore);
                if (hits > 0) continue;
                if (_player != null && Vector3.Distance(p, _player.position) < 15f) continue;
                Spawn(n);
                return;
            }
        }

        void Spawn(LaneNode at)
        {
            var next = at.next[0].transform.position;
            Vector3 dir = next - at.transform.position; dir.y = 0f;
            var rot = Quaternion.LookRotation(dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.forward, Vector3.up);
            int pick = PickPrefab();
            VehicleController v = null;
            for (int i = 0; i < _pool.Count; i++)
                if (_pool[i] != null && _pool[i].name.StartsWith(prefabs[pick].name)) { v = _pool[i]; _pool.RemoveAt(i); break; }
            if (v == null)
            {
                v = Instantiate(prefabs[pick], at.transform.position, rot, transform);
                v.name = prefabs[pick].name + " (bot)";
            }
            else
            {
                v.transform.SetPositionAndRotation(at.transform.position, rot);
                v.gameObject.SetActive(true);
            }
            v.playerDrivable = false;
            VehicleLayers.SetColliderLayers(v.transform, VehicleLayers.Vehicle, null);
            var drv = v.GetComponent<TrafficDriver>();
            if (drv == null) drv = v.gameObject.AddComponent<TrafficDriver>();
            drv.Begin(graph, at);
            _alive.Add(v);
        }

        void Recycle(int index)
        {
            var v = _alive[index];
            _alive.RemoveAt(index);
            v.SetDriver(VehicleDriver.None);
            v.gameObject.SetActive(false);
            _pool.Add(v);
        }

        int PickPrefab()
        {
            if (weights == null || weights.Length != prefabs.Length) return Random.Range(0, prefabs.Length);
            float total = 0f; foreach (var w in weights) total += Mathf.Max(0f, w);
            float r = Random.value * total;
            for (int i = 0; i < weights.Length; i++) { r -= Mathf.Max(0f, weights[i]); if (r <= 0f) return i; }
            return prefabs.Length - 1;
        }
    }
}
