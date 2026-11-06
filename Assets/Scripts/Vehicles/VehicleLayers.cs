// M3 layer names (docs/specs/vehicles-m3.md section 3). Vehicle, DrivenVehicle and
// Pedestrian are layers 14 to 16 in ProjectSettings/TagManager.asset. Road and
// pavement surfaces use the Oshodi thread's "Ground" layer (8), so there is no
// separate Road layer. Resolved by name at runtime so a missing layer logs once.

using UnityEngine;

namespace NaijaLife.Vehicles
{
    public static class VehicleLayers
    {
        public const string VehicleName = "Vehicle";
        public const string DrivenVehicleName = "DrivenVehicle";
        public const string PedestrianName = "Pedestrian";
        public const string GroundName = "Ground"; // road surfaces (Oshodi thread, layer 8)

        static int _vehicle = -2, _driven = -2, _ped = -2, _ground = -2;
        static bool _warned;

        public static int Vehicle => Resolve(ref _vehicle, VehicleName);
        public static int DrivenVehicle => Resolve(ref _driven, DrivenVehicleName);
        public static int Pedestrian => Resolve(ref _ped, PedestrianName);
        public static int Ground => Resolve(ref _ground, GroundName);


        static int Resolve(ref int cache, string name)
        {
            if (cache != -2) return cache;
            cache = LayerMask.NameToLayer(name);
            if (cache < 0)
            {
                if (!_warned)
                {
                    _warned = true;
                    Debug.LogWarning("[VehicleLayers] Layer '" + name + "' is missing. Add Vehicle, DrivenVehicle, " +
                                     "Pedestrian (and the Oshodi Ground layer) in Project Settings > Tags and Layers (spec section 3).");
                }
                cache = -1;
            }
            return cache;
        }

        /// <summary>Mask bit for a layer index, 0 when the layer is missing (-1).</summary>
        public static int Bit(int layer) => layer >= 0 ? 1 << layer : 0;

        /// <summary>Layer index safe to assign to a GameObject (Default when missing).</summary>
        public static int Assignable(int layer) => layer >= 0 ? layer : 0;

        /// <summary>
        /// What a traffic sensor sees (spec 8.2): other vehicles, the player's car,
        /// people, and static world. Ground (road surfaces) is excluded so a ramp is
        /// not a wall; CameraIgnore (skyline shells, never collide) too.
        /// </summary>
        public static int SenseMask
        {
            get
            {
                int m = ~0;
                m &= ~Bit(Ground);
                m &= ~Bit(LayerMask.NameToLayer("CameraIgnore"));
                m &= ~Bit(LayerMask.NameToLayer("Ignore Raycast"));
                return m;
            }
        }

        /// <summary>What blocks a person standing at an exit point (spec 7.2).</summary>
        public static int ExitBlockMask => ~(Bit(DrivenVehicle) | Bit(LayerMask.NameToLayer("Ignore Raycast")) | Bit(LayerMask.NameToLayer("CameraIgnore")));

        public static bool IsPersonLayer(int layer) => Pedestrian >= 0 && layer == Pedestrian;

        /// <summary>Moves every collider under root to a layer, recording the old ones.</summary>
        public static void SetColliderLayers(Transform root, int layer, System.Collections.Generic.Dictionary<GameObject, int> saved)
        {
            foreach (var c in root.GetComponentsInChildren<Collider>(true))
            {
                var go = c.gameObject;
                if (saved != null && !saved.ContainsKey(go)) saved[go] = go.layer;
                go.layer = Assignable(layer);
            }
        }

        public static void RestoreLayers(System.Collections.Generic.Dictionary<GameObject, int> saved)
        {
            if (saved == null) return;
            foreach (var kv in saved)
                if (kv.Key != null) kv.Key.layer = kv.Value;
            saved.Clear();
        }
    }
}
