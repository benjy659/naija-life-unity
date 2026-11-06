// Naija Streets: HUD-7 overlap test. Spec docs/specs/hud-and-phone.md 5.1 and 6.3.
// Shows every one of the 32 right-column combinations (wanted, objective, ride,
// spare rows, toast lane on or off) and checks, for each one, after layout:
//   1. no two HUD pieces overlap (right rows, toasts, wallet row, radar,
//      vitals, prompt, touch buttons, driving pieces);
//   2. every piece stays inside the safe area;
//   3. no piece crosses the screen's vertical midline (left pieces stay left,
//      right pieces stay right: nothing fixed over the middle).
// Run it: Editor menu Naija Streets > UI > HUD-7 overlap test (Play mode), or
// F10 in a development build. The Editor writes the report to
// qa/hud-7/overlap-<w>x<h>-<mode>.txt; a dev build logs it to the console.
// Run once per size (390x844, 844x390, 1280x800, 1920x1080).
// Debug and Editor builds only; a release build has no test.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace NaijaLife.UI
{
    public sealed class NLOverlapTest : MonoBehaviour
    {
        public static bool Running { get; private set; }
        public static string LastReport { get; private set; }
        /// <summary>Called with the report text when a run ends (the Editor writes it to qa/hud-7/).</summary>
        public static event System.Action<string, bool> Finished;

        private static readonly string[] RowIds = { "qa.wanted", "qa.objective", "qa.ride", "qa.spare" };
        private static readonly float[] RowPriority = { NLHudStack.Wanted, NLHudStack.Objective, NLHudStack.Ride, NLHudStack.Spare };
        private static readonly string[] RowText = { "WANTED  3", "Find Musa at Oshodi park", "Ride to Ikeja, 6 min", "Crew: Area Boyz need you" };
        private static readonly string[] RowIcon = { "siren", "route", "car", "people" };

        public static void Run()
        {
            if (Running) return;
            var host = FindFirstObjectByType<NLOverlapTest>();
            if (host == null) host = new GameObject("NL Overlap Test").AddComponent<NLOverlapTest>();
            host.StartCoroutine(host.RunAll());
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (!Running && kb != null && kb.f10Key.wasPressedThisFrame) Run();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindFirstObjectByType<NLOverlapTest>() == null)
            {
                var go = new GameObject("NL Overlap Test");
                go.AddComponent<NLOverlapTest>();
            }
        }

        private IEnumerator RunAll()
        {
            Running = true;
            var ui = NLUIRoot.Instance;
            if (ui == null || !ui.IsBound) { Done("FAIL: no bound NLUIRoot in the scene. Run Add HUD to open scene.", false); yield break; }
            if (NLPhone.IsOpen) NLPhone.Instance?.Close();

            var sb = new StringBuilder();
            sb.Append("HUD-7 overlap test  ").Append(Screen.width).Append('x').Append(Screen.height)
              .Append(" px, scale ").Append(ui.Scale.ToString("0.##")).Append(", mode ").Append(ui.Mode)
              .Append(ui.TouchActive ? ", touch" : ", no touch").Append('\n');
            int failed = 0;

            for (int mask = 0; mask < 32; mask++)
            {
                for (int r = 0; r < 4; r++)
                {
                    if ((mask & (1 << r)) != 0) NLHudStack.Request(RowIds[r], RowPriority[r], Chip(RowIcon[r], RowText[r]));
                    else NLHudStack.Release(RowIds[r]);
                }
                bool toasts = (mask & 16) != 0;
                if (toasts)
                {
                    NLToasts.Show("qa.t1." + mask, NLToastKind.Money, "+₦1,200", "Conductor shift paid");
                    NLToasts.Show("qa.t2." + mask, NLToastKind.People, "Musa dey call", "He get work for you");
                    NLToasts.Show("qa.t3." + mask, NLToastKind.Police, "Police checkpoint", "Slow down for Ikorodu Road");
                }
                // two frames for layout, then the 0.22 s slide-ins
                yield return null; yield return null;
                yield return new WaitForSecondsRealtime(NLUIRoot.ReducedMotion ? 0.05f : 0.3f);

                var problems = Check(ui);
                sb.Append(problems.Count == 0 ? "PASS " : "FAIL ").Append(Describe(mask));
                if (problems.Count > 0) { failed++; foreach (var p in problems) sb.Append("\n     ").Append(p); }
                sb.Append('\n');

                // let this combination's toasts go before the next one
                if (toasts) yield return new WaitForSecondsRealtime(3.8f);
            }
            foreach (var id in RowIds) NLHudStack.Release(id);

            sb.Append(failed == 0 ? "RESULT: PASS, 32 of 32 combinations clear\n" : "RESULT: FAIL, " + failed + " of 32 combinations overlap\n");
            Done(sb.ToString(), failed == 0);
        }

        private static void Done(string report, bool pass)
        {
            Running = false;
            LastReport = report;
            if (pass) Debug.Log("[NLOverlapTest]\n" + report); else Debug.LogWarning("[NLOverlapTest]\n" + report);
            Finished?.Invoke(report, pass);
        }

        private static string Describe(int mask)
        {
            var parts = new List<string>();
            string[] names = { "wanted", "objective", "ride", "spare", "toasts" };
            for (int i = 0; i < 5; i++) if ((mask & (1 << i)) != 0) parts.Add(names[i]);
            return mask.ToString("00") + " [" + (parts.Count == 0 ? "wallet only" : string.Join(", ", parts)) + "]";
        }

        private static VisualElement Chip(string icon, string text)
        {
            var chip = new VisualElement { pickingMode = PickingMode.Ignore };
            chip.AddToClassList("nl-chip");
            NLPhone.Icon(chip, icon).AddToClassList("nl-icon--small");
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            l.AddToClassList("nl-busy-text");
            chip.Add(l);
            return chip;
        }

        private struct Piece { public string Name; public Rect R; public int Side; } // side -1 left, +1 right, 0 either

        private static List<string> Check(NLUIRoot ui)
        {
            var root = ui.Root;
            var pieces = new List<Piece>();
            System.Action<VisualElement, string, int> Add = (e, name, side) =>
            {
                if (e == null || !Visible(e)) return;
                var r = e.worldBound;
                if (float.IsNaN(r.width) || r.width < 1f || r.height < 1f) return;
                pieces.Add(new Piece { Name = name, R = r, Side = side });
            };

            Add(root.Q(className: "nl-row--top"), "wallet row", 1);
            var rows = root.Q("nl-rows");
            if (rows != null) for (int i = 0; i < rows.childCount; i++) Add(rows[i], "row " + i + RowName(rows[i]), 1);
            var lane = root.Q("nl-toasts");
            if (lane != null) for (int i = 0; i < lane.childCount; i++) Add(lane[i], "toast " + i, 1);
            Add(root.Q("nl-radar-wrap"), "radar", -1);
            Add(root.Q("nl-vitals"), "vitals", -1);
            Add(root.Q("nl-prompt"), "prompt", -1);
            foreach (var n in new[] { "nl-btn-phone", "nl-btn-camera", "nl-btn-sprint", "nl-btn-handbrake" }) Add(root.Q(n), n.Substring(3), 1);
            Add(root.Q("nl-speedo"), "speedo", 1);
            Add(root.Q("nl-keyhints"), "key hints", 1);
            Add(root.Q("nl-stick-base"), "stick", -1);

            var problems = new List<string>();
            var safe = ui.Safe != null ? ui.Safe.worldBound : root.worldBound;
            if (ui.Safe != null)
            {
                var sr = ui.Safe.resolvedStyle;
                safe = Rect.MinMaxRect(safe.xMin + sr.paddingLeft, safe.yMin + sr.paddingTop, safe.xMax - sr.paddingRight, safe.yMax - sr.paddingBottom);
            }
            float mid = root.worldBound.center.x;
            const float eps = 0.5f;

            for (int i = 0; i < pieces.Count; i++)
            {
                var a = pieces[i];
                if (a.R.xMin < safe.xMin - eps || a.R.yMin < safe.yMin - eps || a.R.xMax > safe.xMax + eps || a.R.yMax > safe.yMax + eps)
                    problems.Add(a.Name + " leaves the safe area " + Fmt(a.R));
                if (a.Side > 0 && a.R.xMin < mid - eps) problems.Add(a.Name + " crosses the middle " + Fmt(a.R));
                if (a.Side < 0 && a.Name != "stick" && a.R.xMax > mid + eps) problems.Add(a.Name + " crosses the middle " + Fmt(a.R));
                for (int j = i + 1; j < pieces.Count; j++)
                {
                    var b = pieces[j];
                    var inter = Rect.MinMaxRect(Mathf.Max(a.R.xMin, b.R.xMin), Mathf.Max(a.R.yMin, b.R.yMin),
                                                Mathf.Min(a.R.xMax, b.R.xMax), Mathf.Min(a.R.yMax, b.R.yMax));
                    if (inter.width > eps && inter.height > eps)
                        problems.Add(a.Name + " overlaps " + b.Name + " by " + inter.width.ToString("0") + "x" + inter.height.ToString("0") + " px");
                }
            }
            return problems;
        }

        private static string RowName(VisualElement e)
        {
            var l = e.Q<Label>();
            return l != null && !string.IsNullOrEmpty(l.text) ? " (" + l.text + ")" : "";
        }

        private static bool Visible(VisualElement e)
        {
            for (var p = e; p != null; p = p.parent)
            {
                var s = p.resolvedStyle;
                if (s.display == DisplayStyle.None || s.visibility == Visibility.Hidden || s.opacity < 0.05f) return false;
            }
            return true;
        }

        private static string Fmt(Rect r) => "(" + r.x.ToString("0") + "," + r.y.ToString("0") + " " + r.width.ToString("0") + "x" + r.height.ToString("0") + ")";
    }
}
#endif
