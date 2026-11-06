// Naija Streets: the player keep-out box (HUD-4). Spec docs/specs/hud-and-phone.md 6.3.
//   - Each frame: the player's CharacterController bounds, projected through
//     Camera.main, plus 16 px padding = the keep-out box (panel px, top-left origin).
//   - Other parts ask it: NLPrompts moves the prompt to its fallback place
//     (top centre under the top row), NLToasts hides the toast lane and keeps
//     the queue, NLTouchControls rests the stick 24 px further left and down.
//   - In normal camera views nothing fires; it exists for the Top camera,
//     cutscenes and tiny screens.
//   - Debug builds only: F9 draws every rect (keep-out green, stick zone blue,
//     buttons, prompt, toasts) for QA screenshots.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace NaijaLife.UI
{
    public sealed class NLScreenGuard : NLUIPart
    {
        public static NLScreenGuard Instance { get; private set; }

        public const float Padding = 16f;

        /// <summary>Keep-out box in panel px (top-left origin). Empty when the player is off screen or unknown.</summary>
        public static Rect KeepOut { get; private set; }
        public static bool HasKeepOut { get; private set; }

        /// <summary>Fires when HasKeepOut flips or the box moves more than 8 px.</summary>
        public static event System.Action Changed;

        private CharacterController _body;
        private Collider _fallbackBody;
        private float _nextFind;
        private Rect _lastSent;
        private bool _lastHas;
        private readonly Vector3[] _corners = new Vector3[8];

        // debug overlay
        private VisualElement _debug;
        private readonly List<VisualElement> _debugBoxes = new List<VisualElement>();
        private bool _debugOn;

        private void Awake() { Instance = this; }
        private void OnDestroy()
        {
            if (Instance == this) { Instance = null; HasKeepOut = false; }
        }

        /// <summary>True when the element's on-screen rect touches the keep-out box.</summary>
        public static bool Touches(VisualElement e)
        {
            if (!HasKeepOut || e == null || e.panel == null) return false;
            var r = e.worldBound;
            if (float.IsNaN(r.width) || r.width <= 0f || r.height <= 0f) return false;
            return r.Overlaps(KeepOut);
        }

        public static bool Touches(Rect panelRect) => HasKeepOut && panelRect.width > 0f && panelRect.Overlaps(KeepOut);

        protected override void OnBind(VisualElement root)
        {
            _debug = new VisualElement { name = "nl-guard-debug", pickingMode = PickingMode.Ignore };
            _debug.AddToClassList("nl-guard-debug");
            root.Add(_debug);
            _debug.style.display = _debugOn ? DisplayStyle.Flex : DisplayStyle.None;
            _debugBoxes.Clear();
        }

        protected override void OnUnbind()
        {
            _debug?.RemoveFromHierarchy();
            _debug = null;
            Set(false, default);
        }

        protected override void Tick()
        {
            Measure();

            if (Debug.isDebugBuild)
            {
                var kb = Keyboard.current;
                if (kb != null && kb.f9Key.wasPressedThisFrame)
                {
                    _debugOn = !_debugOn;
                    _debug.style.display = _debugOn ? DisplayStyle.Flex : DisplayStyle.None;
                }
                if (_debugOn) DrawDebug();
            }
        }

        private void Measure()
        {
            var cam = Camera.main;
            if (_body == null && _fallbackBody == null && Time.unscaledTime >= _nextFind)
            {
                _nextFind = Time.unscaledTime + 1f;
                var pc = FindFirstObjectByType<NaijaLife.Player.PlayerController>();
                if (pc != null)
                {
                    _body = pc.GetComponent<CharacterController>();
                    if (_body == null) _fallbackBody = pc.GetComponentInChildren<Collider>();
                }
            }
            Bounds b;
            if (_body != null && _body.enabled) b = _body.bounds;
            else if (_fallbackBody != null && _fallbackBody.enabled) b = _fallbackBody.bounds;
            else { Set(false, default); return; }
            if (cam == null || UI == null) { Set(false, default); return; }

            Vector3 c = b.center, e = b.extents;
            int k = 0;
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                        _corners[k++] = new Vector3(c.x + e.x * x, c.y + e.y * y, c.z + e.z * z);

            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                Vector3 sp = cam.WorldToScreenPoint(_corners[i]);
                if (sp.z <= 0f) { Set(false, default); return; } // behind the camera: not on screen
                minX = Mathf.Min(minX, sp.x); maxX = Mathf.Max(maxX, sp.x);
                minY = Mathf.Min(minY, sp.y); maxY = Mathf.Max(maxY, sp.y);
            }
            // screen px (bottom-left) -> panel px (top-left); 1 panel px = Scale screen px
            float s = Mathf.Max(0.01f, UI.Scale);
            float left = minX / s, right = maxX / s;
            float top = (Screen.height - maxY) / s, bottom = (Screen.height - minY) / s;
            var box = Rect.MinMaxRect(left - Padding, top - Padding, right + Padding, bottom + Padding);
            float w = Screen.width / s, h = Screen.height / s;
            if (box.xMax < 0f || box.yMax < 0f || box.xMin > w || box.yMin > h) { Set(false, default); return; }
            Set(true, box);
        }

        private void Set(bool has, Rect box)
        {
            HasKeepOut = has;
            KeepOut = has ? box : default;
            bool moved = has && (Mathf.Abs(box.x - _lastSent.x) > 8f || Mathf.Abs(box.y - _lastSent.y) > 8f
                                 || Mathf.Abs(box.width - _lastSent.width) > 8f || Mathf.Abs(box.height - _lastSent.height) > 8f);
            if (has != _lastHas || moved)
            {
                _lastHas = has;
                _lastSent = box;
                Changed?.Invoke();
            }
        }

        // ---------------- F9 overlay (debug builds) ----------------

        private void DrawDebug()
        {
            int n = 0;
            if (HasKeepOut) Box(n++, KeepOut, "nl-guard-box--keepout");
            n = Add(n, "nl-stick-zone", "nl-guard-box--stick");
            n = Add(n, "nl-btn-sprint", "nl-guard-box--button");
            n = Add(n, "nl-btn-camera", "nl-guard-box--button");
            n = Add(n, "nl-btn-phone", "nl-guard-box--button");
            n = Add(n, "nl-btn-handbrake", "nl-guard-box--button");
            n = Add(n, "nl-prompt", "nl-guard-box--prompt");
            n = Add(n, "nl-toasts", "nl-guard-box--toast");
            n = Add(n, "nl-wallet", "nl-guard-box--chip");
            n = Add(n, "nl-radar-wrap", "nl-guard-box--chip");
            for (int i = n; i < _debugBoxes.Count; i++) _debugBoxes[i].style.display = DisplayStyle.None;
        }

        private int Add(int n, string name, string cls)
        {
            var e = Root.Q(name);
            if (e == null || e.resolvedStyle.display == DisplayStyle.None) return n;
            var r = e.worldBound;
            if (float.IsNaN(r.width) || r.width <= 0f) return n;
            Box(n, r, cls);
            return n + 1;
        }

        private void Box(int i, Rect panelRect, string cls)
        {
            while (_debugBoxes.Count <= i)
            {
                var b = new VisualElement { pickingMode = PickingMode.Ignore };
                b.AddToClassList("nl-guard-box");
                _debug.Add(b);
                _debugBoxes.Add(b);
            }
            var box = _debugBoxes[i];
            box.style.display = DisplayStyle.Flex;
            // _debug sits at the root's top-left, so world rects map 1:1 after removing the root offset
            var o = _debug.worldBound.position;
            box.style.left = panelRect.x - o.x; box.style.top = panelRect.y - o.y;
            box.style.width = panelRect.width; box.style.height = panelRect.height;
            if (box.userData as string != cls)
            {
                if (box.userData is string old) box.RemoveFromClassList(old);
                box.AddToClassList(cls);
                box.userData = cls;
            }
        }
    }
}
