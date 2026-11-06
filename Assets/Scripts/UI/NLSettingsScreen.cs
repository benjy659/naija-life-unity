// Naija Streets (Unity rebuild) — NLSettingsScreen
// Phone > Settings app. Spec section 8.5. The settings live in the game,
// so the phone app is a mirror of the same settings. Wired to concrete
// actions on first use; "coming soon" items show NLStrings.SettingsSoon.

using System;
using UnityEngine.UIElements;

namespace NaijaLife.UI
{
    public class NLSettingsScreen
    {
        public class SettingItem
        {
            public string Title;
            public string Subtitle;
            public bool IsToggle;
            public bool ToggleValue;
            public Action<bool> OnToggle;
            public Action OnTap;
        }

        private readonly VisualElement _root;
        private readonly VisualElement _list;

        public NLSettingsScreen(VisualElement root)
        {
            _root = root;
            _list = root.Q<VisualElement>("nl-settings-list");
        }

        public VisualElement Root => _root;

        public void Build(SettingsScreenModel model)
        {
            _list.Clear();
            foreach (var item in model.Items)
                _list.Add(BuildRow(item));
        }

        private VisualElement BuildRow(SettingItem item)
        {
            var row = new VisualElement();
            row.AddToClassList("nl-settings-row");
            var texts = new VisualElement();
            texts.AddToClassList("nl-settings-texts");
            var title = new Label(item.Title);
            title.AddToClassList("nl-text-value");
            texts.Add(title);
            if (!string.IsNullOrEmpty(item.Subtitle))
            {
                var sub = new Label(item.Subtitle);
                sub.AddToClassList("nl-text-body");
                texts.Add(sub);
            }
            row.Add(texts);
            if (item.IsToggle)
            {
                var toggle = new Button();
                toggle.AddToClassList("nl-settings-toggle");
                toggle.EnableInClassList("nl-settings-toggle--on", item.ToggleValue);
                bool value = item.ToggleValue;
                toggle.clicked += () =>
                {
                    value = !value;
                    toggle.EnableInClassList("nl-settings-toggle--on", value);
                    item.OnToggle?.Invoke(value);
                };
                row.Add(toggle);
            }
            else
            {
                var chevron = new VisualElement();
                chevron.AddToClassList("nl-icon");
                NLIcon.Set(chevron, "chevron");
                row.Add(chevron);
                row.RegisterCallback<ClickEvent>(_ => item.OnTap?.Invoke());
            }
            return row;
        }
    }

    public class SettingsScreenModel
    {
        public System.Collections.Generic.List<NLSettingsScreen.SettingItem> Items =
            new System.Collections.Generic.List<NLSettingsScreen.SettingItem>();
    }
}
