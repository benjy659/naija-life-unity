// Naija Streets: device facts the UI needs (pixel ratio, reduced motion, vibration, touch).
// On WebGL these come from Assets/Plugins/WebGL/NLDevice.jslib; elsewhere from Unity.
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NaijaLife.UI
{
    public static class NLDevice
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern double NLGetDevicePixelRatio();
        [DllImport("__Internal")] private static extern int NLPrefersReducedMotion();
        [DllImport("__Internal")] private static extern void NLVibrate(int ms);
        [DllImport("__Internal")] private static extern int NLHasTouch();
#endif

        /// <summary>Device pixels per CSS pixel. 1 USS px = 1 CSS px uses this as the panel scale.</summary>
        public static float PixelRatio
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return Mathf.Clamp((float)NLGetDevicePixelRatio(), 1f, 4f);
#else
                float dpi = Screen.dpi;
                return dpi <= 0f ? 1f : Mathf.Clamp(dpi / 96f, 1f, 4f);
#endif
            }
        }

        public static bool PrefersReducedMotion
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return NLPrefersReducedMotion() == 1;
#else
                return false;
#endif
            }
        }

        /// <summary>True when the device can be touched (phone, tablet, touch laptop).</summary>
        public static bool HasTouch
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return NLHasTouch() == 1 || Touchscreen.current != null;
#else
                return Touchscreen.current != null;
#endif
            }
        }

        /// <summary>Short tap buzz (Android browsers; iOS Safari has none, which is fine).</summary>
        public static void Tap(int ms = 10)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            NLVibrate(ms);
#elif UNITY_ANDROID || UNITY_IOS
            // Handheld.Vibrate is too long for a tap; skip until a native haptics plugin exists.
#endif
        }
    }
}
