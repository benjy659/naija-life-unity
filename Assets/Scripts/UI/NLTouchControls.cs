// Naija Streets: touch controls in UI Toolkit (HUD-2).
// Spec: docs/specs/hud-and-phone.md section 5.2.
//   - Floating stick: a touch anywhere in #nl-stick-zone puts the base under
//     the thumb; release eases it back to its rest place. 52 px travel,
//     12% dead zone, output 0..1 into Move through NLStickControl.
//   - Look drag is NOT here: PlayerInput (M1.4, lead thread c93f618) already
//     orbits from a right-half touch drag above the button band.
//   - Sprint (hold, or tap-to-toggle from Settings), camera (cycles views),
//     phone. Buttons fire on press, not release.
//   - Two thumbs at once work: each control captures its own pointer id.
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using NaijaLife.Player;

namespace NaijaLife.UI
{
    [RequireComponent(typeof(NLStickControl))]
    public sealed class NLTouchControls : NLUIPart
    {
        public const string SprintToggleKey = "nl.ui.sprintToggle";

        [Tooltip("Thumb travel in CSS px.")]
        [SerializeField] private float stickRadius = 52f;
        [Range(0f, 0.5f)] [SerializeField] private float deadZone = 0.12f;
        [SerializeField] private NLButtonControl sprintControl;

        // Normal, Close, Far (Top view comes with the M2 camera work)
        private static readonly float[] CameraDistances = { 4.5f, 3.0f, 6.5f };

        private NLStickControl _stick;
        private VisualElement _zone, _base, _thumb, _btnSprint, _btnCamera, _btnPhone;
        private int _stickId = -1, _sprintId = -1;
        private Vector2 _stickCentre;
        private bool _sprintLatched;
        private int _cameraIndex;
        private PlayerCamera _camera;

        public static bool SprintToggle
        {
            get => PlayerPrefs.GetInt(SprintToggleKey, 0) == 1;
            set => PlayerPrefs.SetInt(SprintToggleKey, value ? 1 : 0);
        }

        private void Awake()
        {
            _stick = GetComponent<NLStickControl>();
            if (sprintControl == null) sprintControl = GetComponent<NLButtonControl>();
        }

        protected override void OnBind(VisualElement root)
        {
            _zone = root.Q("nl-stick-zone");
            _base = root.Q("nl-stick-base");
            _thumb = root.Q("nl-stick-thumb");
            _btnSprint = root.Q("nl-btn-sprint");
            _btnCamera = root.Q("nl-btn-camera");
            _btnPhone = root.Q("nl-btn-phone");

            _zone.RegisterCallback<PointerDownEvent>(OnStickDown);
            _zone.RegisterCallback<PointerMoveEvent>(OnStickMove);
            _zone.RegisterCallback<PointerUpEvent>(OnStickUp);
            _zone.RegisterCallback<PointerCaptureOutEvent>(OnStickLost);
            _zone.RegisterCallback<GeometryChangedEvent>(_ => { if (_stickId < 0) PlaceStickAtRest(); });

            Hook(_btnSprint, SprintDown, SprintUp);
            Hook(_btnCamera, CycleCamera, null);
            Hook(_btnPhone, () => NLPhone.Instance?.Toggle(), null);

            UI.LayoutChanged += OnLayoutChanged;
        }

        protected override void OnUnbind()
        {
            if (UI != null) UI.LayoutChanged -= OnLayoutChanged;
            ReleaseAll();
        }

        private void OnLayoutChanged()
        {
            if (!UI.TouchActive) ReleaseAll();
            if (_stickId < 0) PlaceStickAtRest();
        }

        protected override void Tick()
        {
            // desktop camera key (V) shares the same cycle
            var kb = Keyboard.current;
            if (kb != null && kb.vKey.wasPressedThisFrame && !NLPhone.IsOpen) CycleCamera();
        }

        // ---------------- stick ----------------

        private void OnStickDown(PointerDownEvent e)
        {
            if (_stickId >= 0 || NLPhone.IsOpen) return;
            _stickId = e.pointerId;
            _zone.CapturePointer(e.pointerId);
            _stickCentre = e.localPosition;
            _base.AddToClassList("is-held");
            _base.style.left = _stickCentre.x - _base.resolvedStyle.width * 0.5f;
            _base.style.top = _stickCentre.y - _base.resolvedStyle.height * 0.5f;
            SetThumb(Vector2.zero);
            NLDevice.Tap();
            e.StopPropagation();
        }

        private void OnStickMove(PointerMoveEvent e)
        {
            if (e.pointerId != _stickId) return;
            Vector2 off = Vector2.ClampMagnitude((Vector2)e.localPosition - _stickCentre, stickRadius);
            SetThumb(off);
            Vector2 v = off / stickRadius;
            float m = v.magnitude;
            v = m < deadZone ? Vector2.zero : v.normalized * Mathf.InverseLerp(deadZone, 1f, m);
            _stick.SetValue(new Vector2(v.x, -v.y)); // panel y grows downward; stick up = forward
        }

        private void OnStickUp(PointerUpEvent e)
        {
            if (e.pointerId != _stickId) return;
            _zone.ReleasePointer(e.pointerId);
            EndStick();
        }

        private void OnStickLost(PointerCaptureOutEvent e)
        {
            if (_stickId >= 0) EndStick();
        }

        private void EndStick()
        {
            _stickId = -1;
            _stick.Release();
            SetThumb(Vector2.zero);
            _base.RemoveFromClassList("is-held");
            PlaceStickAtRest();
        }

        private void SetThumb(Vector2 off)
        {
            _thumb.style.translate = new Translate(off.x, off.y);
        }

        private void PlaceStickAtRest()
        {
            if (_zone == null || float.IsNaN(_zone.layout.height)) return;
            float size = 124f;
            float left = UI != null && UI.Mode == NLScreenMode.Landscape ? 32f : 12f;
            _base.style.left = left;
            _base.style.top = Mathf.Max(0f, _zone.layout.height - 12f - size);
        }

        // ---------------- buttons ----------------

        private void Hook(VisualElement b, System.Action down, System.Action up)
        {
            if (b == null) return;
            b.RegisterCallback<PointerDownEvent>(e =>
            {
                b.CapturePointer(e.pointerId);
                b.AddToClassList("is-down");
                if (b == _btnSprint) _sprintId = e.pointerId;
                NLDevice.Tap();
                down?.Invoke();
                e.StopPropagation();
            });
            void Lift(int id)
            {
                if (b.HasPointerCapture(id)) b.ReleasePointer(id);
                b.RemoveFromClassList("is-down");
                if (b == _btnSprint && _sprintId == id) _sprintId = -1;
                up?.Invoke();
            }
            b.RegisterCallback<PointerUpEvent>(e => Lift(e.pointerId));
            b.RegisterCallback<PointerCaptureOutEvent>(e => { if (b.ClassListContains("is-down")) Lift(e.pointerId); });
        }

        private void SprintDown()
        {
            if (sprintControl == null) return;
            if (SprintToggle)
            {
                _sprintLatched = !_sprintLatched;
                sprintControl.SetDown(_sprintLatched);
                _btnSprint.EnableInClassList("is-latched", _sprintLatched);
            }
            else sprintControl.SetDown(true);
        }

        private void SprintUp()
        {
            if (sprintControl == null || SprintToggle) return;
            sprintControl.SetDown(false);
        }

        private void CycleCamera()
        {
            var cam = Cam();
            if (cam == null) return;
            _cameraIndex = (_cameraIndex + 1) % CameraDistances.Length;
            cam.walkDistance = CameraDistances[_cameraIndex];
        }

        private PlayerCamera Cam()
        {
            if (_camera == null) _camera = FindFirstObjectByType<PlayerCamera>();
            return _camera;
        }

        private void ReleaseAll()
        {
            if (_stickId >= 0) EndStick();
            if (sprintControl != null && !_sprintLatched) sprintControl.SetDown(false);
            _btnSprint?.RemoveFromClassList("is-down");
        }
    }
}
