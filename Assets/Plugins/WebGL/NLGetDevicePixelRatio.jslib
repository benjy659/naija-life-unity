// Naija Streets (Unity rebuild) — NLGetDevicePixelRatio.jslib
// Reads window.devicePixelRatio on WebGL so NLUIScaler can set
// PanelSettings.scale and keep 1 USS px = 1 CSS px. Spec section 3.3.

mergeInto(LibraryManager.library, {
  NLGetDevicePixelRatio: function () {
    return (typeof window !== 'undefined' && window.devicePixelRatio) ? window.devicePixelRatio : 1;
  }
});
