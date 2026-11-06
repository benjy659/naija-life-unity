// Naija Streets: tiny browser bridge for the UI (spec docs/specs/hud-and-phone.md 3.3, 4.5).
mergeInto(LibraryManager.library, {
  NLGetDevicePixelRatio: function () {
    return (typeof window !== 'undefined' && window.devicePixelRatio) ? window.devicePixelRatio : 1;
  },
  NLPrefersReducedMotion: function () {
    try {
      return (window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches) ? 1 : 0;
    } catch (e) { return 0; }
  },
  NLVibrate: function (ms) {
    try { if (navigator.vibrate) navigator.vibrate(ms); } catch (e) {}
  },
  NLHasTouch: function () {
    try { return (('ontouchstart' in window) || navigator.maxTouchPoints > 0) ? 1 : 0; } catch (e) { return 0; }
  }
});
