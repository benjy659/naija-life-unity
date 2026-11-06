// Naija Streets (Unity rebuild) — NLScreenGuard
// Spec section 6.3. Each frame it projects the player's capsule bounds to
// a screen rect plus 16 px padding (the keep-out box) and knows the stick
// and button rects. If the prompt, a toast or any chip would touch one of
// those, it moves to its fallback place (prompt: top centre under the top
// row; toast: hidden and kept in the queue) until clear. In normal camera
// views this never fires; it exists for the Top camera, cutscenes and tiny
// screens. Debug build only: F9 draws every rect for QA screenshots.

using UnityEngine;
using UnityEngine.UIElements;

namespace NaijaLife.UI
{
    public class NLScreenGuard
    {
        private const float PaddingPx = 16f;
        private const float CapsuleHeight = 1.8f;

        private readonly Camera _camera;
        private readonly Transform _player;
        private readonly VisualElement _promptPanel;
        private readonly NLToastManager _toasts;
        private readonly VisualElement _debugOverlay;
        private readonly VisualElement _stickBase;
        private readonly VisualElement[] _buttons;

        private VisualElement _dbgKeepOut;
        private VisualElement _dbgStick;
        private VisualElement _dbgLook;
        private bool _debugOn;

        public Rect KeepOutRect { get; private set; }
        public bool PromptInFallback { get; private set; }

        public NLScreenGuard(Camera camera, Transform player, VisualElement promptPanel,
            NLToastManager toasts, VisualElement debugOverlay, VisualElement stickBase,
            params VisualElement[] buttons)
        {
            _camera = camera;
            _player = player;
            _promptPanel = promptPanel;
            _toasts = toasts;
            _debugOverlay = debugOverlay;
            _stickBase = stickBase;
            _buttons = buttons;
            BuildDebugElements();
        }

        public void SetDebug(bool on)
        {
            _debugOn = on;
            if (_debugOverlay != null) _debugOverlay.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void Tick()
        {
            if (_camera == null || _player == null) return;
            KeepOutRect = ProjectCapsule();

            // Prompt fallback: if the prompt rect touches the keep-out box or
            // any touch button, move it to top centre under the top row.
            bool promptOverlap = false;
            if (_promptPanel != null && _promptPanel.style.display != DisplayStyle.None)
            {
                Rect pr = _promptPanel.worldBound;
                if (Overlaps(pr, KeepOutRect)) promptOverlap = true;
                foreach (var b in _buttons)
                    if (b != null && b.style.display != DisplayStyle.None && Overlaps(pr, b.worldBound))
                        promptOverlap = true;
            }
            PromptInFallback = promptOverlap;
            _promptPanel?.EnableInClassList("nl-prompt--fallback", promptOverlap);

            // Toasts: hide the lane (queue kept) while it would cover the player.
            bool toastOverlap = false;
            if (_toasts != null && _toasts.LiveCount > 0)
            {
                // Toast lane sits under the right column; it only matters when
                // the player is on the right side of the screen.
                toastOverlap = KeepOutRect.xMin > Screen.width * 0.5f;
            }
            _toasts?.SetLaneHidden(toastOverlap);

            if (_debugOn) UpdateDebugOverlay();
        }

        private Rect ProjectCapsule()
        {
            Vector3 feet = _player.position;
            Vector3 head = feet + Vector3.up * CapsuleHeight;
            Vector3 f = _camera.WorldToScreenPoint(feet);
            Vector3 h = _camera.WorldToScreenPoint(head);
            if (f.z < 0f || h.z < 0f) return new Rect(-1000, -1000, 0, 0);
            float x = Mathf.Min(f.x, h.x) - PaddingPx;
            float yTop = Screen.height - Mathf.Max(f.y, h.y) - PaddingPx;
            float w = Mathf.Abs(f.x - h.x) + 48f + PaddingPx * 2f;
            float hgt = Mathf.Abs(f.y - h.y) + PaddingPx * 2f;
            return new Rect(x, yTop, w, hgt);
        }

        private static bool Overlaps(Rect a, Rect b)
        {
            return a.Overlaps(b);
        }

        private void BuildDebugElements()
        {
            if (_debugOverlay == null) return;
            _dbgKeepOut = MakeDebugBox("player keep-out", "nl-debug--keepout");
            _dbgStick = MakeDebugBox("stick zone", "nl-debug--stick");
            _dbgLook = MakeDebugBox("look drag zone", "nl-debug--look");
            _debugOverlay.style.display = DisplayStyle.None;
        }

        private VisualElement MakeDebugBox(string label, string cls)
        {
            var box = new VisualElement();
            box.AddToClassList("nl-debug-box");
            box.AddToClassList(cls);
            var l = new Label(label);
            l.AddToClassList("nl-debug-label");
            box.Add(l);
            _debugOverlay.Add(box);
            return box;
        }

        private void UpdateDebugOverlay()
        {
            PositionDebug(_dbgKeepOut, KeepOutRect);
            if (_stickBase != null)
            {
                Rect sr = _stickBase.worldBound;
                PositionDebug(_dbgStick, new Rect(sr.x - 100, sr.y - 100, sr.width + 200, sr.height + 200));
            }
            PositionDebug(_dbgLook, new Rect(0, 0, Screen.width, Screen.height));
        }

        private void PositionDebug(VisualElement el, Rect r)
        {
            if (el == null) return;
            el.style.left = r.x;
            el.style.top = r.y;
            el.style.width = r.width;
            el.style.height = r.height;
        }
    }
}
