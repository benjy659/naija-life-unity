// M3 player driving input (docs/specs/vehicles-m3.md section 12). On the player;
// enabled by DriveSession only while driving. Reads keyboard / gamepad into a
// DriveInput and hands it to the vehicle. The actions are built in code so the
// M1 PlayerInput.inputactions asset is untouched. Touch: the HUD's floating
// stick (NLTouchControls) already feeds <Gamepad>/leftStick, so on a phone the
// one stick drives: X steers, push up for gas, pull down for brake/reverse
// (stickPedals). Drive/Get out is the HUD prompt (VehiclePromptBridge).
//
//   Desktop: W/Up gas, S/Down brake-reverse, A/D or Left/Right steer, Space handbrake
//   Gamepad: RT gas, LT brake-reverse, left stick X steer (Y up gas / down brake), RB handbrake
//   Exit is DriveSession's Interact action (F / Y-north).
//
// UNVERIFIED: no Unity Editor in Claude's container (2026-11-06).

using UnityEngine;
using UnityEngine.InputSystem;

namespace NaijaLife.Vehicles
{
    [DisallowMultipleComponent]
    public sealed class VehiclePlayerDriver : MonoBehaviour
    {
        [Tooltip("Seconds for keyboard steering to go from centre to full lock (keys are 0 or 1).")]
        public float keyboardSteerRamp = 0.25f;

        [Tooltip("Left stick Y also works the pedals (the phone's touch stick has no triggers).")]
        public bool stickPedals = true;

        [Tooltip("Stick Y dead zone before it counts as gas or brake.")]
        public float stickPedalDeadZone = 0.35f;

        public VehicleController Vehicle { get; private set; }

        /// <summary>Tests and the M3 harness can drive instead of the devices.</summary>
        public bool HasOverride { get; private set; }
        DriveInput _override;

        InputAction _gas, _brake, _steer, _handbrake, _stickY;
        float _steerSmoothed;

        void Awake()
        {
            _gas = new InputAction("Gas", InputActionType.Value);
            _gas.AddBinding("<Keyboard>/w");
            _gas.AddBinding("<Keyboard>/upArrow");
            _gas.AddBinding("<Gamepad>/rightTrigger");

            _brake = new InputAction("Brake", InputActionType.Value);
            _brake.AddBinding("<Keyboard>/s");
            _brake.AddBinding("<Keyboard>/downArrow");
            _brake.AddBinding("<Gamepad>/leftTrigger");

            _steer = new InputAction("Steer", InputActionType.Value);
            _steer.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/a").With("Positive", "<Keyboard>/d");
            _steer.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/leftArrow").With("Positive", "<Keyboard>/rightArrow");
            _steer.AddBinding("<Gamepad>/leftStick/x");

            _stickY = new InputAction("StickPedals", InputActionType.Value);
            _stickY.AddBinding("<Gamepad>/leftStick/y");

            _handbrake = new InputAction("Handbrake", InputActionType.Button);
            _handbrake.AddBinding("<Keyboard>/space");
            _handbrake.AddBinding("<Gamepad>/rightShoulder"); // not South: South accepts the HUD prompt
        }

        void OnDestroy()
        {
            _gas?.Dispose(); _brake?.Dispose(); _steer?.Dispose(); _handbrake?.Dispose(); _stickY?.Dispose();
        }

        public void Begin(VehicleController vehicle)
        {
            Vehicle = vehicle;
            _steerSmoothed = 0f;
            enabled = true;
            _gas.Enable(); _brake.Enable(); _steer.Enable(); _handbrake.Enable(); _stickY.Enable();
        }

        public void End()
        {
            _gas.Disable(); _brake.Disable(); _steer.Disable(); _handbrake.Disable(); _stickY.Disable();
            if (Vehicle != null) Vehicle.Park();
            Vehicle = null;
            enabled = false;
        }

        public void SetOverride(DriveInput input) { _override = input; HasOverride = true; }
        public void ClearOverride() { HasOverride = false; }

        void Update()
        {
            if (Vehicle == null) return;
            if (HasOverride) { Vehicle.SetInput(_override); return; }

            float steerRaw = _steer.ReadValue<float>();
            // Keyboard gives 0/1: ease it so the wheel does not snap. A stick passes through.
            bool digital = Mathf.Approximately(Mathf.Abs(steerRaw), 1f) || Mathf.Approximately(steerRaw, 0f);
            _steerSmoothed = digital
                ? Mathf.MoveTowards(_steerSmoothed, steerRaw, Time.deltaTime / Mathf.Max(0.01f, keyboardSteerRamp))
                : steerRaw;

            float gas = _gas.ReadValue<float>();
            float brake = _brake.ReadValue<float>();
            if (stickPedals)
            {
                float y = _stickY.ReadValue<float>();
                float k = Mathf.Clamp01((Mathf.Abs(y) - stickPedalDeadZone) / Mathf.Max(0.01f, 1f - stickPedalDeadZone));
                if (y > 0f) gas = Mathf.Max(gas, k); else brake = Mathf.Max(brake, k);
            }

            Vehicle.SetInput(new DriveInput
            {
                Throttle = gas,
                Brake = brake,
                Steer = _steerSmoothed,
                Handbrake = _handbrake.IsPressed()
            });
        }
    }
}
