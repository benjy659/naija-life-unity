// Naija Streets: owner of the one UIDocument that holds the whole screen UI.
// Spec: docs/specs/hud-and-phone.md section 3.
//   - 1 USS px = 1 CSS px: PanelSettings.scale = device pixel ratio x size factor.
//   - Safe area padding on #nl-safe (iPhone notch and home bar).
//   - Mode classes on #nl-root: nl-portrait / nl-landscape / nl-desktop.
//   - nl-touch while touch controls are active (auto switch, no flicker).
//   - nl-reduced-motion when the player turned motion down.
// Every other UI component asks NLUIRoot.Instance for the root element.

using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace NaijaLife.UI
{
    public enum NLScreenMode { Portrait, Landscape, Desktop }

    [RequireComponent(typeof(UIDocument))]
    public sealed class NLUIRoot : MonoBehaviour
    {
        public static NLUIRoot Instance { get; private set; }

        /// <summary>Fires after the root is bound and every time the mode or touch state changes.</summary>
        public event Action LayoutChanged;

        public VisualElement Root { get; private set; }
        public VisualElement Safe { get; private set; }
        public NLScreenMode Mode { get; private set; } = NLScreenMode.Desktop;
        public bool TouchActive { get; private set; }
        public float Scale { get; private set; } = 1f;

        private const string ReducedMotionKey = "nl.ui.reducedMotion";

        private UIDocument _doc;
        private PanelSettings _panel;
        private Vector2Int _lastScreen;
        private Rect _lastSafe;
        private int _keyVotes;     // keyboard/mouse must act twice in a row to hide touch controls
        private float _keyHideAt = -1f;

        public static bool ReducedMotion
        {
            get => PlayerPrefs.GetInt(ReducedMotionKey, NLDevice.PrefersReducedMotion ? 1 : 0) == 1;
            set
            {
                PlayerPrefs.SetInt(ReducedMotionKey, value ? 1 : 0);
                if (Instance != null) Instance.ApplyMotionClass();
            }
        }

        private void Awake()
        {
            Instance = this;
            _doc = GetComponent<UIDocument>();
            // Work on a copy so play mode never writes into the PanelSettings asset.
            if (_doc.panelSettings != null)
            {
                _panel = Instantiate(_doc.panelSettings);
                _panel.name = _doc.panelSettings.name + " (runtime)";
                _panel.scaleMode = PanelScaleMode.ConstantPixelSize;
                _doc.panelSettings = _panel;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_panel != null) Destroy(_panel);
        }

        private void OnEnable()
        {
            Root = null; // UIDocument rebuilds its tree on enable; bind again on the next Update
        }

        private void OnDisable()
        {
            Root?.UnregisterCallback<PointerDownEvent>(OnAnyPointerDown, TrickleDown.TrickleDown);
            Root = null;
        }

        private void Bind()
        {
            Root = _doc.rootVisualElement?.Q("nl-root");
            Safe = Root?.Q("nl-safe");
            if (Root == null)
            {
                if (!_warned && _doc.rootVisualElement != null)
                {
                    _warned = true;
                    Debug.LogWarning("[NLUIRoot] #nl-root not found. Is NLHud.uxml set as the UIDocument source?", this);
                }
                return;
            }
            // Pointer type tells us touch vs mouse; trickle down so we see it first.
            Root.RegisterCallback<PointerDownEvent>(OnAnyPointerDown, TrickleDown.TrickleDown);
            SetTouch(NLDevice.HasTouch && (Application.isMobilePlatform || Mouse.current == null), force: true);
            ApplyMotionClass();
            _lastScreen = Vector2Int.zero; // force a layout pass on this frame
            IsBound = true;
        }

        /// <summary>True once #nl-root is found. Other UI components bind after this.</summary>
        public bool IsBound { get; private set; }
        private bool _warned;

        private void OnAnyPointerDown(PointerDownEvent e)
        {
            if (e.pointerType == UnityEngine.UIElements.PointerType.touch || e.pointerType == UnityEngine.UIElements.PointerType.pen)
            {
                // a finger on the glass is unambiguous: show touch controls at once
                _keyVotes = 0;
                _keyHideAt = -1f;
                SetTouch(true);
            }
            else if (e.pointerType == UnityEngine.UIElements.PointerType.mouse)
            {
                // a mouse click hides them, but only on the second click in a row (no flicker)
                if (TouchActive && ++_keyVotes >= 2) _keyHideAt = Time.unscaledTime + 0.22f;
            }
        }

        private void Update()
        {
            if (Root == null) { IsBound = false; Bind(); if (Root == null) return; }

            // a touch anywhere (also on the open world, where no UI element is under
            // the finger) switches touch controls on; Device Simulator sends these too
            var ts = Touchscreen.current;
            if (ts != null && ts.primaryTouch.press.wasPressedThisFrame)
            {
                _keyVotes = 0;
                _keyHideAt = -1f;
                if (!TouchActive) SetTouch(true);
            }

            // keyboard use hides touch controls 0.22 s later (two key presses in a row)
            var kb = Keyboard.current;
            if (TouchActive && kb != null && kb.anyKey.wasPressedThisFrame)
            {
                if (++_keyVotes >= 2) _keyHideAt = Time.unscaledTime + 0.22f;
            }
            if (_keyHideAt > 0f && Time.unscaledTime >= _keyHideAt)
            {
                _keyHideAt = -1f;
                SetTouch(false);
            }

            var size = new Vector2Int(Screen.width, Screen.height);
            if (size != _lastScreen || Screen.safeArea != _lastSafe)
            {
                _lastScreen = size;
                _lastSafe = Screen.safeArea;
                ApplyScale();
                ApplySafeArea();
                ApplyMode();
            }
        }

        private void ApplyScale()
        {
            if (_panel == null) return;
            float ratio = NLDevice.PixelRatio;
            float shortCss = Mathf.Min(Screen.width, Screen.height) / ratio;
            float factor = shortCss < 360f ? 0.9f : (shortCss >= 1000f ? 1.15f : 1f);
            Scale = ratio * factor;
            _panel.scale = Scale;
        }

        private void ApplySafeArea()
        {
            if (Safe == null) return;
            Rect s = Screen.safeArea;
            float k = 1f / Mathf.Max(0.01f, Scale);
            Safe.style.paddingLeft = s.xMin * k;
            Safe.style.paddingRight = (Screen.width - s.xMax) * k;
            Safe.style.paddingTop = (Screen.height - s.yMax) * k;   // Screen.safeArea is bottom-left origin
            Safe.style.paddingBottom = s.yMin * k;
        }

        private void ApplyMode()
        {
            float w = Screen.width / Scale, h = Screen.height / Scale;
            NLScreenMode m;
            if (w > h && h <= 500f) m = NLScreenMode.Landscape;
            else if (!TouchActive && w > 900f) m = NLScreenMode.Desktop;
            else if (w > h) m = NLScreenMode.Desktop; // wide touch tablet: desktop layout, touch controls still on
            else m = NLScreenMode.Portrait;

            Mode = m;
            Root.EnableInClassList("nl-portrait", m == NLScreenMode.Portrait);
            Root.EnableInClassList("nl-landscape", m == NLScreenMode.Landscape);
            Root.EnableInClassList("nl-desktop", m == NLScreenMode.Desktop);
            LayoutChanged?.Invoke();
        }

        private void SetTouch(bool on, bool force = false)
        {
            if (!force && on == TouchActive) return;
            TouchActive = on;
            Root?.EnableInClassList("nl-touch", on);
            if (Root != null) ApplyMode();
        }

        private void ApplyMotionClass()
        {
            Root?.EnableInClassList("nl-reduced-motion", ReducedMotion);
        }

        /// <summary>Add or remove a class on the root (for example nl-phone-open).</summary>
        public void SetRootClass(string cls, bool on) => Root?.EnableInClassList(cls, on);
    }
}
