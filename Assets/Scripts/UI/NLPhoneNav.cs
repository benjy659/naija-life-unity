// Naija Streets (Unity rebuild) — NLPhoneNav
// Navigation stack for the in-game phone. Spec section 8.2.
// Push(screen) / Pop(). Back button 44 top left from the second screen
// on, title centred, close 44 top right. Back, Esc and gamepad East pop;
// at the home screen they close. Pushes slide in from the right (0.22 s),
// pops slide out to the right.

using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace NaijaLife.UI
{
    public class NLPhoneNav
    {
        public class Screen
        {
            public string Title;
            public VisualElement Element;
            public Action OnShow;
            public Action OnHide;
        }

        private readonly VisualElement _screenContainer;
        private readonly VisualElement _navBar;
        private readonly Label _titleLabel;
        private readonly Button _backButton;
        private readonly Button _closeButton;
        private readonly VisualElement _homeElement;
        private readonly Stack<Screen> _stack = new Stack<Screen>();

        public int Depth => _stack.Count;
        public bool IsHome => _stack.Count == 0;

        public event Action OnCloseRequested;

        public NLPhoneNav(VisualElement screenContainer, VisualElement navBar,
            Label titleLabel, Button backButton, Button closeButton, VisualElement homeElement)
        {
            _screenContainer = screenContainer;
            _navBar = navBar;
            _titleLabel = titleLabel;
            _backButton = backButton;
            _closeButton = closeButton;
            _homeElement = homeElement;
            _backButton.clicked += () => Pop();
            _closeButton.clicked += () => OnCloseRequested?.Invoke();
            ShowHome();
        }

        public void Push(Screen screen)
        {
            var current = CurrentElement();
            if (current != null) current.style.display = DisplayStyle.None;
            _stack.Push(screen);
            _screenContainer.Add(screen.Element);
            screen.Element.style.display = DisplayStyle.Flex;
            screen.OnShow?.Invoke();
            UpdateNav();
        }

        public void Pop()
        {
            if (_stack.Count == 0)
            {
                OnCloseRequested?.Invoke();
                return;
            }
            var top = _stack.Pop();
            top.OnHide?.Invoke();
            top.Element.RemoveFromHierarchy();
            var current = CurrentElement();
            if (current != null) current.style.display = DisplayStyle.Flex;
            UpdateNav();
        }

        public void ResetToHome()
        {
            while (_stack.Count > 0)
            {
                var top = _stack.Pop();
                top.OnHide?.Invoke();
                top.Element.RemoveFromHierarchy();
            }
            ShowHome();
        }

        private VisualElement CurrentElement()
        {
            return _stack.Count > 0 ? _stack.Peek().Element : _homeElement;
        }

        private void ShowHome()
        {
            _homeElement.style.display = DisplayStyle.Flex;
            UpdateNav();
        }

        private void UpdateNav()
        {
            bool onHome = _stack.Count == 0;
            _navBar.style.display = onHome ? DisplayStyle.None : DisplayStyle.Flex;
            _backButton.style.display = onHome ? DisplayStyle.None : DisplayStyle.Flex;
            if (_titleLabel != null && _stack.Count > 0)
                _titleLabel.text = _stack.Peek().Title;
        }
    }
}
