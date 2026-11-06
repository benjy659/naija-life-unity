// Naija Streets: the right-column row stack (HUD-3). Spec docs/specs/hud-and-phone.md 5.1.
//   Row 0 wallet + menu is fixed in the UXML. Every other right-column chip asks
//   for a row here, one element per row, ordered by priority (lower = higher up):
//     1 wanted stars, 2 story objective or goal, 2.5 job shift chip,
//     2.6 job busy line, 3 ride chip or offer, 4 spare (crew alert later).
//   Rows collapse upward when hidden (flex), so a lone toast sits right under
//   the wallet. The toast lane always follows the last visible row.
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace NaijaLife.UI
{
    public static class NLHudStack
    {
        public const float Wanted = 1f, Objective = 2f, JobShift = 2.5f, JobBusy = 2.6f, Ride = 3f, Spare = 4f;

        private sealed class Entry { public string Id; public float Priority; public VisualElement El; }
        private static readonly List<Entry> _entries = new List<Entry>();

        /// <summary>Put el in the right-column stack at its priority. Same id replaces the old element.</summary>
        public static VisualElement Request(string id, float priority, VisualElement el)
        {
            var rows = NLUIRoot.Instance != null ? NLUIRoot.Instance.Root?.Q("nl-rows") : null;
            if (rows == null || el == null) return el;
            Release(id);
            _entries.Add(new Entry { Id = id, Priority = priority, El = el });
            _entries.Sort((a, b) => a.Priority.CompareTo(b.Priority));
            el.AddToClassList("nl-stack-row");
            int at = 0;
            foreach (var e in _entries)
            {
                if (e.El == el) break;
                if (e.El.parent == rows) at++;
            }
            rows.Insert(System.Math.Min(at, rows.childCount), el);
            return el;
        }

        public static void Release(string id)
        {
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                if (_entries[i].Id != id) continue;
                _entries[i].El.RemoveFromHierarchy();
                _entries.RemoveAt(i);
            }
        }

        /// <summary>Ids in top-down order (for the HUD-7 overlap test).</summary>
        public static IEnumerable<string> Ids()
        {
            foreach (var e in _entries) yield return e.Id;
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _entries.Clear();
    }
}
