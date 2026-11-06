// Naija Streets: bridges from UI Toolkit touch controls into the Input System.
// The Input System's own On-Screen Stick/Button are uGUI components, so the
// UI Toolkit stick (NLTouchControls) drives these instead. They feed the SAME
// actions as a gamepad (<Gamepad>/leftStick -> Move, <Gamepad>/leftStickPress
// -> Sprint), so PlayerInput needs no second move path.
// Spec: docs/specs/hud-and-phone.md section 5.2.
using UnityEngine;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.OnScreen;

namespace NaijaLife.UI
{
    public sealed class NLStickControl : OnScreenControl
    {
        [InputControl(layout = "Vector2")]
        [SerializeField] private string stickControlPath = "<Gamepad>/leftStick";

        protected override string controlPathInternal
        {
            get => stickControlPath;
            set => stickControlPath = value;
        }

        public void SetValue(Vector2 v) => SendValueToControl(Vector2.ClampMagnitude(v, 1f));
        public void Release() => SendValueToControl(Vector2.zero);
    }

    public sealed class NLButtonControl : OnScreenControl
    {
        [InputControl(layout = "Button")]
        [SerializeField] private string buttonControlPath = "<Gamepad>/leftStickPress";

        protected override string controlPathInternal
        {
            get => buttonControlPath;
            set => buttonControlPath = value;
        }

        public bool IsDown { get; private set; }

        public void SetDown(bool down)
        {
            if (down == IsDown) return;
            IsDown = down;
            SendValueToControl(down ? 1f : 0f);
        }
    }
}
