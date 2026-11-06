// Naija Streets (Unity rebuild) — NLUIKeys
// Polls keyboard/mouse/gamepad for the UI keys the HUD and phone need
// (P phone, M map, V camera, Esc menu/back, E accept, Tab offer chooser).
// The gameplay actions (Move/Look/Sprint/Walk) stay in PlayerInput's
// Gameplay map; this component only reads UI keys directly so no shared
// .inputactions asset has to change. Also reports the last used device to
// NLUIRoot for the touch/desktop switch.

using UnityEngine;
using UnityEngine.InputSystem;

namespace NaijaLife.UI
{
    public class NLUIKeys : MonoBehaviour
    {
        public event System.Action OnPhoneToggle;
        public event System.Action OnMapToggle;
        public event System.Action OnCameraCycle;
        public event System.Action OnMenuToggle;
        public event System.Action OnAccept;
        public event System.Action OnChooserToggle;
        public event System.Action OnDebugOverlayToggle;

        [Tooltip("NLUIRoot that tracks the last used device.")]
        [SerializeField] private NLUIRoot uiRoot;

        private Keyboard _keyboard;
        private Gamepad _gamepad;
        private Mouse _mouse;

        private void Update()
        {
            _keyboard = Keyboard.current;
            _mouse = Mouse.current;
            _gamepad = Gamepad.current;

            if (_keyboard != null)
            {
                if (_keyboard.anyKey.wasPressedThisFrame) uiRoot?.NoteInputDevice(NLDeviceKind.KeyboardMouse);
                if (_keyboard.pKey.wasPressedThisFrame) OnPhoneToggle?.Invoke();
                if (_keyboard.mKey.wasPressedThisFrame) OnMapToggle?.Invoke();
                if (_keyboard.vKey.wasPressedThisFrame) OnCameraCycle?.Invoke();
                if (_keyboard.escapeKey.wasPressedThisFrame) OnMenuToggle?.Invoke();
                if (_keyboard.eKey.wasPressedThisFrame) OnAccept?.Invoke();
                if (_keyboard.tabKey.wasPressedThisFrame) OnChooserToggle?.Invoke();
                if (_keyboard.f9Key.wasPressedThisFrame) OnDebugOverlayToggle?.Invoke();
            }
            if (_mouse != null && (_mouse.leftButton.wasPressedThisFrame || _mouse.rightButton.wasPressedThisFrame))
                uiRoot?.NoteInputDevice(NLDeviceKind.KeyboardMouse);
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
                uiRoot?.NoteInputDevice(NLDeviceKind.Touch);
            if (_gamepad != null)
            {
                if (_gamepad.selectButton.wasPressedThisFrame) OnPhoneToggle?.Invoke();
                if (_gamepad.buttonEast.wasPressedThisFrame) OnMenuToggle?.Invoke();
                if (_gamepad.buttonSouth.wasPressedThisFrame) OnAccept?.Invoke();
                if (_gamepad.anyKey.wasPressedThisFrame) uiRoot?.NoteInputDevice(NLDeviceKind.Gamepad);
            }
        }
    }
}
