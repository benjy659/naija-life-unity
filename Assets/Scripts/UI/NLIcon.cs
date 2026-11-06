// Naija Streets (Unity rebuild) — NLIcon
// One helper for every icon in the HUD and phone. Spec section 4.4.
// Icons are white-stroke SVGs (24 grid, 1.8 stroke, round caps) imported as
// VectorImage. The HUD controller registers them at Awake from its
// serialized list (assigned by the scene builder from Assets/UI/Icons),
// so runtime code never touches AssetDatabase or Resources.
// Static UXML icons use background-image url('project://database/...').

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace NaijaLife.UI
{
    public static class NLIcon
    {
        private static readonly Dictionary<string, VectorImage> Registry =
            new Dictionary<string, VectorImage>();

        /// <summary>Called by NLHudController at Awake from its serialized icon list.</summary>
        public static void Register(string name, VectorImage image)
        {
            if (string.IsNullOrEmpty(name) || image == null) return;
            Registry[name] = image;
        }

        /// <summary>Set a VisualElement's background to the named icon (VectorImage).</summary>
        public static void Set(VisualElement element, string iconName)
        {
            if (element == null || string.IsNullOrEmpty(iconName)) return;
            if (Registry.TryGetValue(iconName, out var img) && img != null)
                element.style.backgroundImage = new StyleBackground(img);
        }

        public static VectorImage Get(string iconName)
        {
            Registry.TryGetValue(iconName, out var img);
            return img;
        }
    }
}
