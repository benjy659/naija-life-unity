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

        private void Update()
        {
            if (!IsReady)
                return;

            MoveInput = _moveAction.ReadValue<Vector2>();
            LookInput = _lookAction.ReadValue<Vector2>();
            SprintHeld = _sprintAction.IsPressed();
            WalkHeld = _walkAction.IsPressed();
        }
    }
}
