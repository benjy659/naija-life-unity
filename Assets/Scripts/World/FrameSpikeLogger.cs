// M2.3 frame spike logger (docs/specs/oshodi-greybox.md section 7).
// Keeps the worst frame time in a 10 s window that starts when a district
// load or unload starts. Budget: no frame over 200 ms entering a district.
// Shows the result on a small overlay (Editor and development builds only)
// and logs one line per window, so QA can read it from the browser console.

using System.Collections.Generic;
using UnityEngine;

namespace NaijaLife.World
{
    public class FrameSpikeLogger : MonoBehaviour
    {
        public static FrameSpikeLogger Instance { get; private set; }

        public float windowSeconds = 10f;
        public float budgetMs = 200f;
        public bool showOverlay = true;

        class Window
        {
            public string label;
            public float endsAt;
            public float worstMs;
            public float worstAt;
        }

        readonly List<Window> _open = new List<Window>();
        readonly List<string> _results = new List<string>();
        float _worstEverMs;
        float _smoothedMs;

        public string StreamerState { get; set; } = "";

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // Called by DistrictStreamer when a load or unload starts.
        public void BeginWindow(string label)
        {
            _open.Add(new Window { label = label, endsAt = Time.realtimeSinceStartup + windowSeconds });
        }

        void Update()
        {
            // unscaledDeltaTime is the real time the last frame took, pauses included.
            float ms = Time.unscaledDeltaTime * 1000f;
            _smoothedMs = Mathf.Lerp(_smoothedMs, ms, 0.1f);
            float now = Time.realtimeSinceStartup;
            for (int i = _open.Count - 1; i >= 0; i--)
            {
                var w = _open[i];
                if (ms > w.worstMs) { w.worstMs = ms; w.worstAt = now; }
                if (ms > _worstEverMs) _worstEverMs = ms;
                if (now < w.endsAt) continue;
                bool ok = w.worstMs <= budgetMs;
                string line = $"{w.label}: worst frame {w.worstMs:F0} ms ({(ok ? "OK" : "OVER " + budgetMs.ToString("F0") + " ms")})";
                _results.Add(line);
                if (_results.Count > 6) _results.RemoveAt(0);
                if (ok) Debug.Log("[FrameSpike] " + line);
                else Debug.LogWarning("[FrameSpike] " + line);
                _open.RemoveAt(i);
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        GUIStyle _style;

        void OnGUI()
        {
            if (!showOverlay) return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 12 };
                _style.normal.textColor = Color.white;
            }
            var text = $"{_smoothedMs:F1} ms   worst in a window {_worstEverMs:F0} ms\n{StreamerState}";
            if (_open.Count > 0) text += $"\nmeasuring: {_open[0].label} ({_open[0].endsAt - Time.realtimeSinceStartup:F0} s)";
            foreach (var r in _results) text += "\n" + r;
            GUI.Box(new Rect(10, Screen.height - 150, 420, 140), text, _style);
        }
#endif
    }
}
