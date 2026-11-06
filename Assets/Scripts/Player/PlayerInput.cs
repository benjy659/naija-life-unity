// Naija Streets (Unity rebuild) — M1.1 Player Input
// Spec: docs/MIGRATION-PLAN.md §6 M1 and docs/DESIGN-INVENTORY.md §2.
//   ONE Input System action map ("Gameplay") drives BOTH control schemes:
//     - Keyboard/mouse: WASD (or arrows) to move, mouse delta to look,
//       Left/Right Shift to sprint, Left Ctrl (or C) to walk.
//     - Touch (mobile): an on-screen joystick (Unity Input System
//       On-Screen Stick component, added by the UI work) writes into the
//       SAME "Move" action — there is deliberately no second move path and
//       no old Input Manager usage anywhere in this project.
//     - Gamepad (bonus, same map): left stick move, right stick look,
//       left-stick press sprint.
//   The mobile joystick binds in the .inputactions asset's Touch control
//   scheme; the On-Screen Stick UI component is what feeds touch deltas
//   into the Move action at runtime (it needs no binding line of its own
//   in the asset — it writes to the action directly).
//
// The camera builder (M1.2) consumes LookInput. Movement magnitude is
// 0..1 (joystick tilt) or 1 (keyboard); PlayerController scales speed
// from it, so a light joystick tilt walks and a full tilt jogs/runs.
//
// UNVERIFIED (Nov 5, 2026): no Unity Editor exists on this VM yet, so this
// code has never run. See Assets/Scripts/Player/README_M1.1.md.

using UnityEngine;
using UnityEngine.InputSystem;

namespace NaijaLife.Player
{
    public class PlayerInput : MonoBehaviour
    {
        [Tooltip("Input Action Asset (PlayerInput.inputactions). One map: 'Gameplay'.")]
        [SerializeField] private InputActionAsset inputActions;

        [Tooltip("Name of the action map in the asset. Do not rename without updating the asset.")]
        [SerializeField] private string actionMapName = "Gameplay";

        /// <summary>Move input, camera-relative. X = strafe, Y = forward. Magnitude 0..1.</summary>
        public Vector2 MoveInput { get; private set; }

        /// <summary>Look input (mouse delta / right stick). Consumed by the M1.2 camera.</summary>
        public Vector2 LookInput { get; private set; }

        /// <summary>True while Sprint is held (Shift / stick press).</summary>
        public bool SprintHeld { get; private set; }

        /// <summary>True while Walk is held (Left Ctrl / C).</summary>
        public bool WalkHeld { get; private set; }

        /// <summary>
        /// Camera orbit for this frame, in DEGREES (x = yaw, y = pitch, positive
        /// y = drag up). Already scaled per device and gated by the M1.4 rules:
        /// desktop orbits only while the right mouse button is held (a free
        /// mouse never moves the lens), phone orbits by dragging on the right
        /// half of the screen above the bottom button band, gamepad by the
        /// right stick. PlayerCamera reads this every frame.
        /// </summary>
        public Vector2 OrbitDegrees { get; private set; }

        [Header("Camera orbit (M1.4)")]
        [Tooltip("Mouse orbit, degrees per pixel of drag while the right button is held.")]
        [SerializeField] private float mouseDegreesPerPixel = 0.15f;
        [Tooltip("Touch orbit, degrees per pixel of drag on the right half of the screen.")]
        [SerializeField] private float touchDegreesPerPixel = 0.2f;
        [Tooltip("Gamepad right stick orbit, degrees per second at full deflection.")]
        [SerializeField] private float stickDegreesPerSecond = 120f;
        [Tooltip("Right-stick dead zone.")]
        [SerializeField] private float stickDeadZone = 0.15f;
        [Tooltip("Bottom share of the screen kept for buttons (sprint, phone); touches starting there never orbit.")]
        [Range(0f, 0.6f)]
        [SerializeField] private float touchButtonBand = 0.35f;

        // Touch that owns the orbit drag (-1 = none).
        private int _orbitTouchId = -1;

        /// <summary>True once the action map was found and enabled.</summary>
        public bool IsReady { get; private set; }

        private InputActionMap _actionMap;
        private InputAction _moveAction;
        private InputAction _lookAction;
        private InputAction _sprintAction;
        private InputAction _walkAction;

        private void Awake()
        {
            if (inputActions == null)
            {
                Debug.LogWarning("[PlayerInput] No InputActionAsset assigned — player will not move. " +
                    "Assign PlayerInput.inputactions in the Player prefab.", this);
                return;
            }

            _actionMap = inputActions.FindActionMap(actionMapName, throwIfNotFound: true);
            _moveAction = _actionMap.FindAction("Move", throwIfNotFound: true);
            _lookAction = _actionMap.FindAction("Look", throwIfNotFound: true);
            _sprintAction = _actionMap.FindAction("Sprint", throwIfNotFound: true);
            _walkAction = _actionMap.FindAction("Walk", throwIfNotFound: true);
            IsReady = true;
        }

        private void OnEnable()
        {
            _actionMap?.Enable();
        }

        private void OnDisable()
        {
            _actionMap?.Disable();
        }

        /// <summary>True while a full-screen UI (the phone) owns input. All outputs read zero.</summary>
        public bool IsLocked { get; private set; }

        /// <summary>
        /// HUD-5: the phone locks the player so they stop walking while it is up.
        /// Zeroes every output at once so nothing keeps moving from a stale read.
        /// </summary>
        public void SetLocked(bool locked)
        {
            IsLocked = locked;
            if (!locked) return;
            MoveInput = Vector2.zero;
            LookInput = Vector2.zero;
            OrbitDegrees = Vector2.zero;
            SprintHeld = false;
            WalkHeld = false;
            _orbitTouchId = -1;
        }

        private void Update()
        {
            if (!IsReady || IsLocked)
                return;

            MoveInput = _moveAction.ReadValue<Vector2>();
            LookInput = _lookAction.ReadValue<Vector2>();
            SprintHeld = _sprintAction.IsPressed();
            WalkHeld = _walkAction.IsPressed();
            OrbitDegrees = ReadOrbit();
        }

        // M1.4: orbit from the right mouse drag, a right-half touch drag, or
        // the right stick. Read straight from devices because the rules differ
        // per device (a held button, a screen region, a rate), which one
        // Look action cannot express.
        private Vector2 ReadOrbit()
        {
            Vector2 degrees = Vector2.zero;

            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.isPressed)
                degrees += mouse.delta.ReadValue() * mouseDegreesPerPixel;

            Gamepad pad = Gamepad.current;
            if (pad != null)
            {
                Vector2 stick = pad.rightStick.ReadValue();
                if (stick.magnitude > stickDeadZone)
                    degrees += stick * (stickDegreesPerSecond * Time.deltaTime);
            }

            Touchscreen touch = Touchscreen.current;
            if (touch != null)
                degrees += ReadTouchOrbit(touch);

            return degrees;
        }

        private Vector2 ReadTouchOrbit(Touchscreen screen)
        {
            foreach (var t in screen.touches)
            {
                var phase = t.phase.ReadValue();
                int id = t.touchId.ReadValue();

                if (_orbitTouchId == -1 && phase == UnityEngine.InputSystem.TouchPhase.Began)
                {
                    Vector2 start = t.startPosition.ReadValue();
                    bool rightHalf = start.x > Screen.width * 0.5f;
                    bool aboveButtons = start.y > Screen.height * touchButtonBand;
                    if (rightHalf && aboveButtons)
                        _orbitTouchId = id;
                }

                if (id != _orbitTouchId)
                    continue;

                if (phase == UnityEngine.InputSystem.TouchPhase.Ended ||
                    phase == UnityEngine.InputSystem.TouchPhase.Canceled)
                {
                    _orbitTouchId = -1;
                    return Vector2.zero;
                }
                return t.delta.ReadValue() * touchDegreesPerPixel;
            }
            // Owning touch vanished without an end phase: release it.
            if (_orbitTouchId != -1)
            {
                bool stillThere = false;
                foreach (var t in screen.touches)
                    if (t.touchId.ReadValue() == _orbitTouchId && t.isInProgress) { stillThere = true; break; }
                if (!stillThere) _orbitTouchId = -1;
            }
            return Vector2.zero;
        }
    }
}
