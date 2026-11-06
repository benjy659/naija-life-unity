// Naija Streets (Unity) - PLACEHOLDER job UI so M4 can be played before the
// HUD workstream's UI Toolkit kit lands (docs/specs/hud-and-phone.md). Plain
// IMGUI, scaled to the screen height. Replace with the HUD implementation of
// IJobUi; keep the same behaviour (one prompt, nothing over the player, the
// change sheet at most 35% of the screen height).
// Keys: E accept prompt, Space shout, 1/2/3 pick change, X end shift.

using System;
using System.Collections.Generic;
using NaijaLife.Economy;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NaijaLife.JobsGlue
{
    public sealed class JobDebugUi : MonoBehaviour, IJobUi
    {
        struct PromptRec { public string Id; public PromptKind Kind; public string Verb, Subject, Line; public Action Accept; }
        struct ToastRec { public string Title, Line; public float Until; }

        readonly Dictionary<string, PromptRec> _prompts = new Dictionary<string, PromptRec>();
        readonly List<ToastRec> _toasts = new List<ToastRec>();
        readonly List<(Vector3 pos, string text, float until)> _barks = new List<(Vector3, string, float)>();
        bool _chip; float _chipLeft; int _chipTrips; long _chipPending; string _chipNote;
        ShoutView? _shout; Action _onShout;
        ChangeView? _change; Action<int> _onPick;
        string _busy;
        string _sumTitle, _sumBody; Action _sumClose;
        Texture2D _ring, _disc;
        float _s = 1f;

        void OnEnable() { BotWorker.Barked += OnBark; }
        void OnDisable() { BotWorker.Barked -= OnBark; }

        void OnBark(BotWorker b, string line)
        {
            if (b) _barks.Add((b.transform.position + Vector3.up * 2.1f, line, Time.time + 2.5f));
        }

        public void Offer(string id, PromptKind kind, string verb, string subject, string line, Action onAccept) =>
            _prompts[id] = new PromptRec { Id = id, Kind = kind, Verb = verb, Subject = subject, Line = line, Accept = onAccept };
        public void Withdraw(string id) => _prompts.Remove(id);
        public void Toast(string title, string line)
        {
            _toasts.Insert(0, new ToastRec { Title = title, Line = line, Until = Time.time + 3.5f });
            if (_toasts.Count > 3) _toasts.RemoveAt(_toasts.Count - 1);
        }
        public void ShowShiftChip(bool show, float secondsLeft, int trips, long pending, string note)
        { _chip = show; _chipLeft = secondsLeft; _chipTrips = trips; _chipPending = pending; _chipNote = note; }
        public void ShowShout(ShoutView? view, Action onShout) { _shout = view; _onShout = onShout; }
        public void ShowChange(ChangeView? view, Action<int> onPick) { _change = view; _onPick = onPick; }
        public void ShowBusy(string text) { _busy = text; }
        public void ShowSummary(string title, string body, Action onClose) { _sumTitle = title; _sumBody = body; _sumClose = onClose; }

        bool TopPrompt(out PromptRec best)
        {
            best = default; bool any = false;
            foreach (PromptRec p in new List<PromptRec>(_prompts.Values))
                if (!any || Rank(p.Kind) > Rank(best.Kind)) { best = p; any = true; }
            return any;
        }
        static int Rank(PromptKind k) => k == PromptKind.Exit ? 2 : k == PromptKind.Action ? 1 : 0;

        void Update()
        {
            _toasts.RemoveAll(t => Time.time > t.Until);
            _barks.RemoveAll(b => Time.time > b.until);
#if ENABLE_INPUT_SYSTEM
            Keyboard k = Keyboard.current;
            if (k == null) return;
            if (_sumTitle != null) { if (k.eKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame) CloseSummary(); return; }
            if (k.spaceKey.wasPressedThisFrame && _shout.HasValue) _onShout?.Invoke();
            if (_change.HasValue && _change.Value.Result == null)
            {
                if (k.digit1Key.wasPressedThisFrame) _onPick?.Invoke(0);
                if (k.digit2Key.wasPressedThisFrame) _onPick?.Invoke(1);
                if (k.digit3Key.wasPressedThisFrame) _onPick?.Invoke(2);
            }
            if (k.xKey.wasPressedThisFrame) foreach (PromptRec p in new List<PromptRec>(_prompts.Values)) if (p.Kind == PromptKind.Exit) { p.Accept?.Invoke(); break; }
            if (k.eKey.wasPressedThisFrame)
                foreach (PromptRec p in new List<PromptRec>(_prompts.Values)) if (p.Kind == PromptKind.Action) { p.Accept?.Invoke(); break; }
#endif
        }

        void CloseSummary() { Action a = _sumClose; _sumTitle = null; _sumBody = null; _sumClose = null; a?.Invoke(); }

        void OnGUI()
        {
            _s = Mathf.Max(0.75f, Screen.height / 800f);
            GUI.skin.label.wordWrap = true;
            var title = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = Mathf.RoundToInt(15 * _s) };
            var small = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(12 * _s) };
            var btn = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(14 * _s), fontStyle = FontStyle.Bold };
            float m = 16 * _s;

            // Wallet (top right).
            if (WalletCache.Known)
                GUI.Label(new Rect(Screen.width - 220 * _s - m, m, 220 * _s, 24 * _s),
                    (WalletCache.IsTest ? "TEST  " : "") + "₦" + WalletCache.Balance.ToString("N0"), title);

            // Prompts (top left, one slot; exit prompt as a separate small button).
            float py = m + 40 * _s;
            foreach (PromptRec p in new List<PromptRec>(_prompts.Values))
            {
                if (p.Kind != PromptKind.Exit) continue;
                if (GUI.Button(new Rect(m, py, 150 * _s, 36 * _s), p.Verb + "  (X)", btn)) p.Accept?.Invoke();
                py += 44 * _s;
            }
            foreach (PromptRec p in new List<PromptRec>(_prompts.Values))
            {
                if (p.Kind == PromptKind.Exit) continue;
                Rect r = new Rect(m, py, Mathf.Min(Screen.width * 0.5f - m, 300 * _s), 60 * _s);
                GUI.Box(r, GUIContent.none);
                GUI.Label(new Rect(r.x + 10 * _s, r.y + 6 * _s, r.width - 20 * _s, 22 * _s), p.Verb + "  (E)", title);
                GUI.Label(new Rect(r.x + 10 * _s, r.y + 28 * _s, r.width - 20 * _s, 30 * _s), p.Subject + (string.IsNullOrEmpty(p.Line) ? "" : "   " + p.Line), small);
                if (GUI.Button(r, GUIContent.none, GUIStyle.none)) p.Accept?.Invoke();
                break;   // one action prompt at a time
            }

            // Shift chip (top centre).
            if (_chip)
            {
                Rect r = new Rect(Screen.width * 0.5f - 150 * _s, m, 300 * _s, 46 * _s);
                GUI.Box(r, GUIContent.none);
                int mm = Mathf.Max(0, Mathf.CeilToInt(_chipLeft)) / 60, ss = Mathf.Max(0, Mathf.CeilToInt(_chipLeft)) % 60;
                GUI.Label(new Rect(r.x + 8 * _s, r.y + 2 * _s, r.width - 16 * _s, 22 * _s), $"Shift {mm}:{ss:00}   Trips {_chipTrips}   This shift ₦{_chipPending:N0}", title);
                GUI.Label(new Rect(r.x + 8 * _s, r.y + 24 * _s, r.width - 16 * _s, 20 * _s), _chipNote ?? "", small);
            }

            // Toasts (right side, under the wallet).
            float ty = m + 34 * _s;
            foreach (ToastRec t in _toasts)
            {
                Rect r = new Rect(Screen.width - 280 * _s - m, ty, 280 * _s, 50 * _s);
                GUI.Box(r, GUIContent.none);
                GUI.Label(new Rect(r.x + 8 * _s, r.y + 4 * _s, r.width - 16 * _s, 20 * _s), t.Title, title);
                GUI.Label(new Rect(r.x + 8 * _s, r.y + 24 * _s, r.width - 16 * _s, 24 * _s), t.Line ?? "", small);
                ty += 56 * _s;
            }

            // Bot barks as world labels.
            Camera cam = Camera.main;
            if (cam)
                foreach (var b in _barks)
                {
                    Vector3 sp = cam.WorldToScreenPoint(b.pos);
                    if (sp.z <= 0f) continue;
                    GUI.Label(new Rect(sp.x - 90 * _s, Screen.height - sp.y - 12 * _s, 180 * _s, 24 * _s), b.text, small);
                }

            // Shout button (bottom right, where the action button lives).
            if (_shout.HasValue)
            {
                ShoutView v = _shout.Value;
                float size = 150 * _s;
                Rect area = new Rect(Screen.width - size - m, Screen.height - size - m - 30 * _s, size, size);
                EnsureTextures();
                Color old = GUI.color;
                GUI.color = new Color(0.36f, 0.94f, 0.71f, 0.55f);
                float target = size * 0.4f;
                GUI.DrawTexture(Centered(area, target), _ring);
                GUI.color = v.Gold ? new Color(0.36f, 0.94f, 0.71f) : new Color(0.54f, 0.58f, 0.72f);
                GUI.DrawTexture(Centered(area, target * v.RingScale), _ring);
                GUI.color = old;
                if (GUI.Button(Centered(area, target * 0.9f), "Shout!", btn)) _onShout?.Invoke();
                GUI.Label(new Rect(area.x - 60 * _s, area.yMax + 2 * _s, size + 60 * _s, 26 * _s),
                    $"Seats {v.Seats}/{v.SeatsMax}   {Mathf.CeilToInt(v.SecondsLeft)}s   {v.Say}", small);
            }

            // Change sheet (bottom centre, at most 35% of the screen height).
            if (_change.HasValue)
            {
                ChangeView c = _change.Value;
                float h = Mathf.Min(Screen.height * 0.35f, 170 * _s), w = Mathf.Min(Screen.width - 2 * m, 420 * _s);
                Rect r = new Rect((Screen.width - w) * 0.5f, Screen.height - h - m, w, h);
                GUI.Box(r, GUIContent.none);
                GUI.Label(new Rect(r.x + 10 * _s, r.y + 6 * _s, w - 20 * _s, 22 * _s), $"Collect fares {c.Index + 1} of {c.Of}   {Mathf.CeilToInt(c.SecondsLeft)}s", title);
                GUI.Label(new Rect(r.x + 10 * _s, r.y + 30 * _s, w - 20 * _s, 22 * _s), $"Passenger gives you ₦{c.Note:N0} for a ₦{c.Fare} fare. Give change:", small);
                float bw = (w - 40 * _s) / 3f;
                for (int i = 0; i < c.Options.Length; i++)
                    if (GUI.Button(new Rect(r.x + 10 * _s + i * (bw + 10 * _s), r.y + 58 * _s, bw, 48 * _s), $"₦{c.Options[i]:N0}  ({i + 1})", btn) && c.Result == null)
                        _onPick?.Invoke(i);
                if (c.Result != null) GUI.Label(new Rect(r.x + 10 * _s, r.y + 112 * _s, w - 20 * _s, 24 * _s), c.Result, title);
            }

            if (_busy != null)
                GUI.Label(new Rect(Screen.width * 0.5f - 150 * _s, m + 54 * _s, 300 * _s, 24 * _s), _busy, title);

            if (_sumTitle != null)
            {
                float w = Mathf.Min(Screen.width - 2 * m, 360 * _s), h = 190 * _s;
                Rect r = new Rect((Screen.width - w) * 0.5f, Screen.height * 0.18f, w, h);
                GUI.Box(r, GUIContent.none);
                GUI.Label(new Rect(r.x + 12 * _s, r.y + 10 * _s, w - 24 * _s, 24 * _s), _sumTitle, title);
                GUI.Label(new Rect(r.x + 12 * _s, r.y + 38 * _s, w - 24 * _s, h - 90 * _s), _sumBody, small);
                if (GUI.Button(new Rect(r.x + 12 * _s, r.yMax - 46 * _s, w - 24 * _s, 36 * _s), "OK  (E)", btn)) CloseSummary();
            }
        }

        static Rect Centered(Rect area, float size) => new Rect(area.center.x - size * 0.5f, area.center.y - size * 0.5f, size, size);

        void EnsureTextures()
        {
            if (_ring) return;
            const int n = 128;
            _ring = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = x - n / 2f + 0.5f, dy = y - n / 2f + 0.5f, d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - Mathf.Abs(d - (n / 2f - 4f)) / 3f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            _ring.SetPixels32(px);
            _ring.Apply();
        }

        void OnDestroy() { if (_ring) Destroy(_ring); if (_disc) Destroy(_disc); }
    }
}
