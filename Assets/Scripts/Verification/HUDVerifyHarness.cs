// HUD VERIFICATION HARNESS — temporary, NOT part of the game.
// Drives HUDTest.unity in Editor play mode: checks the HUD renders with
// the fake wallet/game feeds, opens the phone, opens Bank and Map, toggles
// the F9 debug overlay, and captures screenshots. Writes results to
// <outDir>/results.txt and screenshots to <outDir>/shots/.
// Key presses are driven by calling the same handlers NLUIKeys feeds
// (P/E/F9), since synthetic Input System events do not reach legacy
// UnityEngine.Input in headless play mode.

using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;
using NaijaLife.UI;

namespace NaijaLife.Verification
{
    public class HUDVerifyHarness : MonoBehaviour
    {
        public string outDir = "/var/cache/apt/archives/unity/hud-verify/desktop";
        public bool iconSheetRun = false;

        private NLHudController _hud;
        private StringBuilder _log = new StringBuilder();
        private System.Collections.Generic.List<string> _failures = new System.Collections.Generic.List<string>();

        private void Log(string s)
        {
            string line = $"[{Time.time:0.00}] {s}";
            _log.AppendLine(line);
            Debug.Log("[HUDVerify] " + s);
        }

        private void Fail(string s)
        {
            _failures.Add(s);
            Log("FAIL: " + s);
        }

        private void Check(bool ok, string name)
        {
            Log((ok ? "PASS: " : "FAIL: ") + name);
            if (!ok) _failures.Add(name);
        }

        private IEnumerator Start()
        {
            Directory.CreateDirectory(outDir);
            Directory.CreateDirectory(Path.Combine(outDir, "shots"));
            Log($"HUD verify harness started. Screen={Screen.width}x{Screen.height} scale={NLUIScaler.CurrentScale:0.00}");
            yield return new WaitForSeconds(1.5f);

            _hud = FindFirstObjectByType<NLHudController>();
            if (_hud == null) { Fail("No NLHudController in scene"); Finish(); yield break; }
            var root = _hud.Root;
            if (root == null) { Fail("NLHudController root is null"); Finish(); yield break; }

            // Two keyboard presses so NLUIRoot switches from its default Touch
            // device to KeyboardMouse (desktop mode on wide screens).
            yield return SimulateKeyboardDevice();
            yield return new WaitForSeconds(0.5f);

            // ---- HUD element checks ----
            Check(TextOf(root, "nl-wallet-amount") == "₦8,450", $"wallet amount shows ₦8,450 (got '{TextOf(root, "nl-wallet-amount")}')");
            Check(TextOf(root, "nl-place-text") == "OSHODI", $"place pill shows OSHODI (got '{TextOf(root, "nl-place-text")}')");
            Check(TextOf(root, "nl-clock-text") == "07:42", $"clock shows 07:42 (got '{TextOf(root, "nl-clock-text")}')");
            Check(Visible(root, "nl-prompt"), "prompt panel visible (fake offer)");
            Check(Visible(root, "nl-radar"), "radar element visible");
            Check(Visible(root, "nl-vitals"), "vitals bars visible");
            Check(Visible(root, "nl-row-wallet"), "wallet row visible");
            Check(!Visible(root, "nl-row-wanted"), "wanted row hidden at level 0");
            Check(Visible(root, "nl-row-objective"), "objective chip visible");
            Check(!Visible(root, "nl-row-ride"), "ride chip hidden with no ride");
            Check(TextOf(root, "nl-hint-text").Contains("P Phone"), "desktop hint chip copy present");
            if (Screen.width > 900)
                Check(Visible(root, "nl-hint-chip"), "hint chip visible on desktop");
            yield return Shot("01-hud");

            // ---- Prompt accept (same handler the E key feeds) ----
            _hud.Prompts.Accept();
            yield return new WaitForSeconds(0.6f);
            Check(CountToasts(root) >= 1, $"toast appears after prompt accept (count {CountToasts(root)})");
            yield return Shot("02-toast");

            // ---- Phone: open, home ----
            _hud.Phone.Open();
            yield return new WaitForSeconds(0.8f);
            Check(_hud.Phone.IsOpen, "phone open");
            Check(Visible(root, "nl-phone-home"), "phone home visible");
            Check(TextOf(root, "nl-phone-big-clock") == "07:42", "phone clock 07:42");
            yield return Shot("03-phone-home");

            // ---- Phone: Bank app ----
            ClickAppTile(root, "Bank");
            yield return new WaitForSeconds(0.8f);
            Check(TextOf(root, "nl-bank-balance") == "₦8,450", $"bank balance ₦8,450 (got '{TextOf(root, "nl-bank-balance")}')");
            Check(Visible(root, "nl-bank-list"), "bank history list visible");
            yield return Shot("04-phone-bank");

            // ---- Phone: back to home, then Map (M key path) ----
            _hud.Phone.Back();
            yield return new WaitForSeconds(0.4f);
            _hud.Phone.OpenToMap();
            yield return new WaitForSeconds(0.6f);
            Check(TextOf(root, "nl-phone-title") == "Map", $"phone title is Map (got '{TextOf(root, "nl-phone-title")}')");
            yield return Shot("05-phone-map");

            // ---- Phone: close ----
            _hud.Phone.Close();
            yield return new WaitForSeconds(0.8f);
            Check(!_hud.Phone.IsOpen, "phone closed");

            // ---- F9 debug overlay (same handler the F9 key feeds) ----
            _hud.SetDebug(true);
            yield return new WaitForSeconds(0.4f);
            Check(Visible(root, "nl-debug-overlay"), "F9 debug overlay visible");
            yield return Shot("06-debug-overlay");
            _hud.SetDebug(false);

            Finish();
        }

        private IEnumerator SimulateKeyboardDevice()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null)
            {
                Log("No Keyboard.current — device switch not simulated (mode may stay portrait)");
                yield break;
            }
            // V = camera cycle (harmless toggle). Two presses: NLUIRoot needs
            // 2 events in a row from the new device before it switches.
            for (int i = 0; i < 2; i++)
            {
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(kb,
                    new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.V));
                yield return null;
                yield return null;
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(kb,
                    new UnityEngine.InputSystem.LowLevel.KeyboardState());
                yield return null;
            }
            Log("Queued 2x V key presses (device -> KeyboardMouse)");
        }

        private string TextOf(VisualElement root, string name)
        {
            var el = root.Q<Label>(name);
            return el != null ? el.text : "<missing>";
        }

        private bool Visible(VisualElement root, string name)
        {
            var el = root.Q<VisualElement>(name);
            if (el == null) return false;
            // Walk up: hidden if any ancestor is display:none.
            var v = el;
            while (v != null)
            {
                if (v.style.display == DisplayStyle.None) return false;
                v = v.parent;
            }
            return el.resolvedStyle.width > 0 && el.resolvedStyle.height > 0;
        }

        private int CountToasts(VisualElement root)
        {
            var lane = root.Q<VisualElement>("nl-toast-lane");
            return lane != null ? lane.childCount : 0;
        }

        private void ClickAppTile(VisualElement root, string label)
        {
            var grid = root.Q<VisualElement>("nl-phone-grid");
            if (grid == null) { Fail("phone grid missing"); return; }
            foreach (var tile in grid.Query<Button>().ToList())
            {
                var name = tile.Q<Label>();
                if (name != null && name.text == label)
                {
                    tile.SendEvent(new ClickEvent { target = tile });
                    Log($"Clicked app tile '{label}'");
                    return;
                }
            }
            Fail($"app tile '{label}' not found");
        }

        private IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            string path = Path.Combine(outDir, "shots", name + ".png");
            ScreenCapture.CaptureScreenshot(path);
            yield return new WaitForEndOfFrame();
            Log($"Screenshot: {path}");
        }

        private void Finish()
        {
            _log.AppendLine($"FAILURES: {_failures.Count}");
            foreach (var f in _failures) _log.AppendLine(" - " + f);
            _log.AppendLine(_failures.Count == 0 ? "RESULT: ALL HUD CHECKS PASSED" : "RESULT: FAILURES PRESENT (see above)");
            File.WriteAllText(Path.Combine(outDir, "results.txt"), _log.ToString());
            File.WriteAllText(Path.Combine(outDir, "done.txt"), System.DateTime.UtcNow.ToString("o"));
            Debug.Log("[HUDVerify] DONE marker written to " + Path.Combine(outDir, "done.txt"));
        }
    }
}
