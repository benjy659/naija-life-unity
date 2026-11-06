// Naija Streets (Unity rebuild) — NLUIRoot
// Owns the UI Toolkit root: mode classes, safe-area padding, reduced motion,
// and the touch/desktop switch. Spec: docs/specs/hud-and-phone.md §3.
// Mode rule: landscape when width > height and height <= 500; desktop when
// the last used device is keyboard/mouse or gamepad and the screen is wider
// than 900; otherwise portrait. Touch UI shows when the last input came from
// a touchscreen, and hides 0.22 s after a keyboard key or mouse button is
// pressed. Switching needs 2 events in a row from the new device (no flicker).

using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace NaijaLife.UI
{
    public enum NLDeviceKind { Touch, KeyboardMouse, Gamepad }

    public class NLUIRoot : MonoBehaviour
    {
        [Tooltip("UIDocument that owns the HUD visual tree.")]
        [SerializeField] private UIDocument uiDocument;

        /// <summary>True while touch controls are visible.</summary>
        public bool TouchUIActive { get; private set; } = true;

        /// <summary>Player setting: reduce motion (kills slides and count-ups).</summary>
        public bool ReduceMotion { get; private set; }

        /// <summary>Player setting: tap sprint toggles instead of hold.</summary>
        public bool SprintToggle { get; private set; }

        public event System.Action<bool> OnTouchUIChanged;

        private VisualElement _root;
        private NLDeviceKind _lastDevice = NLDeviceKind.Touch;
        private NLDeviceKind _pendingDevice = NLDeviceKind.Touch;
        private int _pendingCount;
        private Coroutine _hideTouchRoutine;
        private bool _modeDirty = true;

        private void Awake()
        {
            if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
            ReduceMotion = PlayerPrefs.GetInt("nl_reduce_motion", 0) == 1;
            SprintToggle = PlayerPrefs.GetInt("nl_sprint_toggle", 0) == 1;
        }

        private void OnEnable()
        {
            if (uiDocument != null && uiDocument.rootVisualElement != null)
                Bind(uiDocument.rootVisualElement);
        }

        public void Bind(VisualElement root)
        {
            _root = root;
            ApplyModeClasses();
            ApplySafeArea();
            ApplyReducedMotion();
            SetTouchUI(_lastDevice == NLDeviceKind.Touch, true);
        }

        private void Update()
        {
            if (_modeDirty)
            {
                ApplyModeClasses();
                ApplySafeArea();
                _modeDirty = false;
            }
        }

        /// <summary>Called by NLUIKeys whenever input arrives from a device.</summary>
        public void NoteInputDevice(NLDeviceKind device)
        {
            if (device == _pendingDevice) _pendingCount++;
            else { _pendingDevice = device; _pendingCount = 1; }
            if (_pendingCount < 2) return;
            if (device == _lastDevice) return;

            _lastDevice = device;
            _modeDirty = true;
            if (device == NLDeviceKind.Touch)
            {
                if (_hideTouchRoutine != null) { StopCoroutine(_hideTouchRoutine); _hideTouchRoutine = null; }
                SetTouchUI(true, false);
            }
            else
            {
                if (_hideTouchRoutine == null)
                    _hideTouchRoutine = StartCoroutine(HideTouchAfterDelay());
            }
        }

        private IEnumerator HideTouchAfterDelay()
        {
            yield return new WaitForSeconds(0.22f);
            _hideTouchRoutine = null;
            SetTouchUI(false, false);
        }

        public void SetReduceMotion(bool on)
        {
            ReduceMotion = on;
            PlayerPrefs.SetInt("nl_reduce_motion", on ? 1 : 0);
            PlayerPrefs.Save();
            ApplyReducedMotion();
        }

        public void SetSprintToggle(bool on)
        {
            SprintToggle = on;
            PlayerPrefs.SetInt("nl_sprint_toggle", on ? 1 : 0);
            PlayerPrefs.Save();
        }

        public void MarkModeDirty() => _modeDirty = true;

        private void ApplyModeClasses()
        {
            if (_root == null) return;
            float w = Screen.width, h = Screen.height;
            bool landscape = w > h && h <= 500f;
            bool desktop = !landscape && _lastDevice != NLDeviceKind.Touch && w > 900f;
            _root.RemoveFromClassList("nl-portrait");
            _root.RemoveFromClassList("nl-landscape");
            _root.RemoveFromClassList("nl-desktop");
            _root.AddToClassList(landscape ? "nl-landscape" : desktop ? "nl-desktop" : "nl-portrait");
        }

        private void ApplySafeArea()
        {
            if (_root == null) return;
            Rect sa = Screen.safeArea;
            float scale = NLUIScaler.CurrentScale;
            if (scale <= 0f) scale = 1f;
            _root.style.paddingLeft = sa.xMin / scale;
            _root.style.paddingTop = sa.yMin / scale;
            _root.style.paddingRight = (Screen.width - sa.xMax) / scale;
            _root.style.paddingBottom = (Screen.height - sa.yMax) / scale;
        }

        private void ApplyReducedMotion()
        {
            if (_root == null) return;
            _root.EnableInClassList("nl-reduced-motion", ReduceMotion);
        }

        private void SetTouchUI(bool on, bool instant)
        {
            if (TouchUIActive == on && !instant) return;
            TouchUIActive = on;
            if (_root != null) _root.EnableInClassList("nl-touch-ui", on);
            OnTouchUIChanged?.Invoke(on);
        }
    }
}
