// M2.3 streaming stub (docs/specs/oshodi-greybox.md section 7).
// One district tile: the Addressables address of its scene and the rectangle
// it covers on the ground (X east, Z north, metres). DistrictStreamer wakes it
// inside wakeDistance and puts it to sleep outside sleepDistance. The 100 m
// gap between the two stops a player standing on the line from loading and
// unloading it over and over.

using UnityEngine;

namespace NaijaLife.World
{
    [CreateAssetMenu(menuName = "Naija Streets/District Tile", fileName = "DistrictTile")]
    public class DistrictTile : ScriptableObject
    {
        public string id = "Oshodi_Core";
        [Tooltip("Addressables address of the district scene.")]
        public string sceneAddress = "District_Oshodi_Core";
        [Tooltip("Tile rectangle on the ground: x = west edge, y = south edge (world Z), width, height.")]
        public Rect bounds = new Rect(-120f, -150f, 240f, 300f);
        public float wakeDistance = 600f;
        public float sleepDistance = 700f;

        // Flat distance from a world point to the tile rectangle (0 inside it).
        public float DistanceTo(Vector3 p)
        {
            float dx = Mathf.Max(bounds.xMin - p.x, 0f, p.x - bounds.xMax);
            float dz = Mathf.Max(bounds.yMin - p.z, 0f, p.z - bounds.yMax);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        void OnValidate()
        {
            if (sleepDistance < wakeDistance + 50f) sleepDistance = wakeDistance + 50f;
        }
    }
}
