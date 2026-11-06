// Naija Streets: one browser WebSocket for Supabase Realtime DMs (M5.3, docs/specs/netcode-m5.md 8.1).
// Events go back to C# with SendMessage on the receiver's GameObject (main thread).
// UNVERIFIED (2026-11-06): not built into WebGL yet.
mergeInto(LibraryManager.library, {
  NLRT_Open: function (urlPtr, goPtr) {
    var url = UTF8ToString(urlPtr), go = UTF8ToString(goPtr);
    var old = window.__nlrt; window.__nlrt = null;
    if (old) { try { old.close(); } catch (e) {} }
    var ws;
    try { ws = new WebSocket(url); } catch (e) { SendMessage(go, 'OnSocketClose', ''); return; }
    window.__nlrt = ws;
    ws.onopen = function () { if (window.__nlrt === ws) SendMessage(go, 'OnSocketOpen', ''); };
    ws.onmessage = function (e) { if (window.__nlrt === ws && typeof e.data === 'string') SendMessage(go, 'OnSocketMessage', e.data); };
    ws.onclose = function () { if (window.__nlrt === ws) { window.__nlrt = null; SendMessage(go, 'OnSocketClose', ''); } };
    ws.onerror = function () {}; // onclose follows
  },
  NLRT_Send: function (textPtr) {
    var ws = window.__nlrt;
    if (ws && ws.readyState === 1) { try { ws.send(UTF8ToString(textPtr)); } catch (e) {} }
  },
  NLRT_Close: function () {
    var ws = window.__nlrt; window.__nlrt = null; // no OnSocketClose for our own close; C# raises it
    if (ws) { try { ws.close(); } catch (e) {} }
  },
  NLRT_IsOpen: function () {
    var ws = window.__nlrt;
    return (ws && ws.readyState === 1) ? 1 : 0;
  }
});
