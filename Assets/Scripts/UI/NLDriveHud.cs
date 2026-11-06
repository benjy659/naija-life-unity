// Naija Streets: driving HUD pieces for M3.4 (vehicles thread asked, 2026-11-06).
// Spec docs/specs/vehicles-m3.md section 12 and hud-and-phone.md section 5.2.
//   - Only while DriveSession.State == Driving: speedo (km/h) and, on touch,
//     a 64 px handbrake button bottom-right. Never in the centre third.
//   - The on-foot buttons hide while driving; the floating stick stays
//     (VehiclePlayerDriver reads it: X steers, up gas, down brake).
//   - Handbrake feeds <Gamepad>/rightShoulder through an NLButtonControl, so
//     the vehicles code needs nothing new.
using UnityEngine;
using UnityEngine.UIElements;
using NaijaLife.Vehicles;

namespace NaijaLife.UI
{
    public sealed class NLDriveHud : NLUIPart
    {
        [Tooltip("NLButtonControl bound to <Gamepad>/rightShoulder. Created at runtime if empty.")]
        [SerializeField] private NLButtonControl handbrake;

        private DriveSession _session;
        private VisualElement _btn;
        private Label _speed;
        private int _shownKmh = -1;
        private bool _driving;
        private float _nextFind;

        private void Awake()
        {
            if (handbrake == null)
            {
                // a child object: OnScreenControl paths are per component, and the
                // sprint NLButtonControl on the UI object uses leftStickPress
                var go = new GameObject("NL Handbrake Control");
                go.transform.SetParent(transform, false);
                go.SetActive(false);
                handbrake = go.AddComponent<NLButtonControl>();
                handbrake.controlPath = "<Gamepad>/rightShoulder";
                go.SetActive(true);
            }
        }

        protected override void OnBind(VisualElement root)
        {
            _speed = root.Q<Label>("nl-speedo-value");
            _btn = root.Q("nl-btn-handbrake");
            _btn.RegisterCallback<PointerDownEvent>(e =>
            {
                _btn.CapturePointer(e.pointerId);
                _btn.AddToClassList("is-down");
                handbrake.SetDown(true);
                NLDevice.Tap();
                e.StopPropagation();
            });
            _btn.RegisterCallback<PointerUpEvent>(e => Lift(e.pointerId));
            _btn.RegisterCallback<PointerCaptureOutEvent>(e => Lift(e.pointerId));
            _driving = false;
            UI.SetRootClass("nl-driving", false);
        }

        protected override void OnUnbind() => handbrake?.SetDown(false);

        private void Lift(int id)
        {
            if (_btn.HasPointerCapture(id)) _btn.ReleasePointer(id);
            _btn.RemoveFromClassList("is-down");
            handbrake.SetDown(false);
        }

        protected override void Tick()
        {
            if (_session == null && Time.unscaledTime >= _nextFind)
            {
                _nextFind = Time.unscaledTime + 1f;
                _session = FindFirstObjectByType<DriveSession>();
            }
            bool driving = _session != null && _session.State == DriveState.Driving && _session.CurrentVehicle != null;
            if (driving != _driving)
            {
                _driving = driving;
                UI.SetRootClass("nl-driving", driving);
                if (!driving) { handbrake.SetDown(false); _btn.RemoveFromClassList("is-down"); }
                _shownKmh = -1;
            }
            if (!driving) return;
            int kmh = Mathf.RoundToInt(_session.CurrentVehicle.SpeedKmh);
            if (kmh != _shownKmh) { _shownKmh = kmh; _speed.text = kmh.ToString(); }
        }
    }
}
