// Naija Streets (Unity rebuild) — NLUIScaler
// Sets PanelSettings.scale so 1 USS px = 1 CSS px of the browser game.
// Spec: docs/specs/hud-and-phone.md section 3.3. PanelSettings scale mode is
// Constant Pixel Size. On WebGL, scale = window.devicePixelRatio through the
// NLGetDevicePixelRatio .jslib; off WebGL, scale = Screen.dpi / 96 clamped
// 1..4 (1 when Screen.dpi is 0). Then a size factor: 0.9 when the short side
// is under 360 CSS px, 1.0 normally, 1.15 when the short side is >= 1000.

using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UIElements;

namespace NaijaLife.UI
{
    public class NLUIScaler : MonoBehaviour
    {
        [Tooltip("PanelSettings used by the HUD UIDocument (Constant Pixel Size).")]
        [SerializeField] private PanelSettings panelSettings;

        [Tooltip("UIDocument whose panel scale is driven.")]
        [SerializeField] private UIDocument uiDocument;

        /// <summary>Scale currently applied (for NLUIRoot safe-area math).</summary>
        public static float CurrentScale { get; private set; } = 1f;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern float NLGetDevicePixelRatio();
#endif

        private float _lastW, _lastH;

        private void Awake()
        {
            if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
            if (panelSettings == null && uiDocument != null) panelSettings = uiDocument.panelSettings;
            Apply();
        }

        private void Update()
        {
            if (Screen.width != _lastW || Screen.height != _lastH)
                Apply();
        }

        public void Apply()
        {
            _lastW = Screen.width;
            _lastH = Screen.height;
            float scale = ComputeScale();
            CurrentScale = scale;
            if (panelSettings != null) panelSettings.scale = scale;
            var root = FindFirstObjectByType<NLUIRoot>();
            if (root != null) root.MarkModeDirty();
        }

        private float ComputeScale()
        {
            float baseScale;
#if UNITY_WEBGL && !UNITY_EDITOR
            baseScale = NLGetDevicePixelRatio();
            if (baseScale <= 0f) baseScale = 1f;
#else
            baseScale = Screen.dpi > 0f ? Screen.dpi / 96f : 1f;
            baseScale = Mathf.Clamp(baseScale, 1f, 4f);
#endif
            float shortSide = Mathf.Min(Screen.width, Screen.height) / baseScale;
            float factor = shortSide < 360f ? 0.9f : shortSide >= 1000f ? 1.15f : 1f;
            return baseScale * factor;
        }
    }
}
