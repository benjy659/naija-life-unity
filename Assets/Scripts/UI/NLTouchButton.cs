// Naija Streets (Unity rebuild) — NLTouchButton
// A UI Toolkit button that fires on PRESS, not release (spec 5.2), with
// hold support (sprint is hold-to-sprint, with a Settings toggle for
// tap-to-toggle). Wraps a VisualElement so the HUD can use plain
// VisualElements (not uGUI) for the sprint, camera and phone buttons.

using System;
using UnityEngine.UIElements;

namespace NaijaLife.UI
{
    public class NLTouchButton
    {
        private readonly VisualElement _element;
        private int _pointerId = -1;

        /// <summary>Fired once on press.</summary>
        public event Action OnPressed;

        /// <summary>Fired on release (hold buttons use this to stop).</summary>
        public event Action OnReleased;

        /// <summary>True while the button is held down.</summary>
        public bool Held { get; private set; }

        public NLTouchButton(VisualElement element)
        {
            _element = element;
            _element.RegisterCallback<PointerDownEvent>(OnPointerDown);
            _element.RegisterCallback<PointerUpEvent>(OnPointerUp);
            _element.RegisterCallback<PointerCancelEvent>(OnPointerUp);
            _element.RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
            _element.RegisterCallback<ClickEvent>(e => e.StopPropagation());
        }

        private void OnPointerDown(PointerDownEvent e)
        {
            if (_pointerId >= 0) return;
            _pointerId = e.pointerId;
            _element.CapturePointer(e.pointerId);
            Held = true;
            _element.AddToClassList("nl-touch-btn--pressed");
            OnPressed?.Invoke();
            e.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent e)
        {
            if (e.pointerId != _pointerId) return;
            Release();
            e.StopPropagation();
        }

        private void OnPointerLeave(PointerLeaveEvent e)
        {
            if (e.pointerId != _pointerId) return;
            Release();
        }

        private void Release()
        {
            if (_pointerId < 0) return;
            _element.ReleasePointer(_pointerId);
            _pointerId = -1;
            Held = false;
            _element.RemoveFromClassList("nl-touch-btn--pressed");
            OnReleased?.Invoke();
        }
    }
}
