// Naija Streets (Unity rebuild) — NLStickControl / NLButtonControl
// Small OnScreenControl subclasses so the UI Toolkit touch controls drive
// the SAME Input System actions as keyboard/gamepad. Spec section 2 and
// 5.2. The Move action already binds <Gamepad>/leftStick (with a
// StickDeadzone processor) and Sprint binds <Gamepad>/leftStickPress, so
// these controls write into those paths. A virtual gamepad is added at
// runtime when no physical gamepad exists (same pattern as the M1 verify
// harness), so the controls work on phones with no gamepad attached.

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Layouts;

namespace NaijaLife.UI
{
    /// <summary>Writes a Vector2 into a stick control (default: the gamepad left stick).</summary>
    public class NLStickControl : OnScreenControl
    {
        [InputControl(layout = "Vector2")]
        [SerializeField] private string controlPath = "<Gamepad>/leftStick";

        protected override string controlPathInternal
        {
            get => controlPath;
            set => controlPath = value;
        }

        public void SetValue(Vector2 value)
        {
            SendValueToControl(value);
        }
    }

    /// <summary>Writes a button press into a button control (default: the gamepad left-stick press, which Sprint binds).</summary>
    public class NLButtonControl : OnScreenControl
    {
        [InputControl(layout = "Button")]
        [SerializeField] private string controlPath = "<Gamepad>/leftStickPress";

        protected override string controlPathInternal
        {
            get => controlPath;
            set => controlPath = value;
        }

        public void SetPressed(bool pressed)
        {
            SendValueToControl(pressed ? 1f : 0f);
        }
    }

    /// <summary>Ensures a gamepad device exists so OnScreenControls have a
    /// control to write into (phones have no physical gamepad).</summary>
    public static class NLVirtualGamepad
    {
        private static Gamepad _virtual;

        public static void Ensure()
        {
            if (Gamepad.current != null) return;
            if (_virtual != null) return;
            _virtual = InputSystem.AddDevice<Gamepad>();
        }
    }
}
