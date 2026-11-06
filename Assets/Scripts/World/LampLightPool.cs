// M2.2 street lamp light pool (docs/specs/oshodi-greybox.md section 6.3).
// Lamp heads are always emissive. At night a small pool of real point lights
// moves to the lamps nearest the camera: 4 / 6 / 8 lights on Low / Medium /
// High quality (index 0 / 1 / 2). Same budget as the browser rule: never more
// than 8 point lights.
//
// Lamps are found by name under the generated root: objects whose name ends in
// "-head" (street lamps) or starts with "soffit-" (under the flyover).

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NaijaLife.World
{
    public class LampLightPool : MonoBehaviour
    {
        public int[] lightsPerQualityLevel = { 4, 6, 8 };
        public float range = 18f;
        public float intensity = 6f;
        public Color colour = new Color(1f, 0.82f, 0.55f);
        [Tooltip("Lights switch on when TimeOfDay.Night01 is above this.")]
        public float nightThreshold = 0.5f;
        public float refreshSeconds = 0.5f;

        readonly List<Vector3> _lamps = new List<Vector3>();
        readonly List<Light> _pool = new List<Light>();
        float _next;
        int _want;

        void Start()
        {
            Rescan();
        }

        // Districts stream in and out (M2.3), so look for lamps again each time.
        void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
        }

        void OnSceneLoaded(Scene s, LoadSceneMode m) => Rescan();
        void OnSceneUnloaded(Scene s) => Rescan();

        /// <summary>Find lamp heads again (call after a district loads).</summary>
        public void Rescan()
        {
            _lamps.Clear();
            foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                var n = t.name;
                if (n.EndsWith("-head") && n.StartsWith("lamp-")) _lamps.Add(t.position + Vector3.down * 0.3f);
                else if (n.StartsWith("soffit-")) _lamps.Add(t.position + Vector3.down * 0.3f);
            }
            int level = Mathf.Clamp(QualitySettings.GetQualityLevel(), 0, lightsPerQualityLevel.Length - 1);
            int want = Mathf.Min(8, lightsPerQualityLevel[level]);
            _want = want;
            while (_pool.Count < want)
            {
                var go = new GameObject("PooledLampLight");
                go.transform.SetParent(transform, false);
                var l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = range;
                l.intensity = intensity;
                l.color = colour;
                l.shadows = LightShadows.None;
                l.enabled = false;
                _pool.Add(l);
            }
            for (int i = want; i < _pool.Count; i++) _pool[i].enabled = false;
        }

        void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + refreshSeconds;
            var tod = TimeOfDay.Instance;
            var cam = Camera.main;
            RefreshNow(tod != null && tod.Night01 > nightThreshold, cam != null ? cam.transform.position : (Vector3?)null);
        }

        /// <summary>Place the pool now. Also works in edit mode (QA capture runner):
        /// call Rescan first there, since Start does not run.</summary>
        public void RefreshNow(bool night, Vector3? camera)
        {
            if (!night || camera == null || _lamps.Count == 0)
            {
                foreach (var l in _pool) l.enabled = false;
                return;
            }

            // pick the nearest lamps to the camera (small lists: a partial selection sort is fine)
            var p = camera.Value;
            var order = new List<int>(_lamps.Count);
            for (int i = 0; i < _lamps.Count; i++) order.Add(i);
            order.Sort((a, b) => (_lamps[a] - p).sqrMagnitude.CompareTo((_lamps[b] - p).sqrMagnitude));
            for (int i = 0; i < _pool.Count; i++)
            {
                bool on = i < order.Count && i < _want;
                _pool[i].enabled = on;
                if (on) _pool[i].transform.position = _lamps[order[i]];
            }
        }
    }
}
