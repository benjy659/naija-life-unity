// Naija Streets (Unity rebuild) — NLHudStack
// Allocates the right-column rows (spec section 5.1): wallet +0, wanted +52,
// objective +100, ride +148, spare +196, toast lane under the last visible
// row + 12. Rows collapse upward when empty, so a lone toast sits right
// under the wallet. One element per row; the 32-combination overlap test in
// HUD-7 must pass.

using System.Collections.Generic;
using UnityEngine.UIElements;

namespace NaijaLife.UI
{
    public class NLHudStack
    {
        public enum RowId { Wallet, Wanted, Objective, Ride, Spare }

        private const int RowHeight = 48;
        private const int RowGap = 4;
        private const int TopOffset = 0;

        private readonly VisualElement _container;
        private readonly Dictionary<RowId, VisualElement> _rows = new Dictionary<RowId, VisualElement>();
        private readonly VisualElement _toastLane;

        public NLHudStack(VisualElement container, VisualElement toastLane)
        {
            _container = container;
            _toastLane = toastLane;
        }

        public void Register(RowId id, VisualElement element)
        {
            _rows[id] = element;
        }

        /// <summary>Recompute positions from visibility. Call after any row shows/hides.</summary>
        public void Layout()
        {
            int y = TopOffset;
            foreach (RowId id in new[] { RowId.Wallet, RowId.Wanted, RowId.Objective, RowId.Ride, RowId.Spare })
            {
                if (!_rows.TryGetValue(id, out var el)) continue;
                bool visible = el.style.display != DisplayStyle.None;
                if (!visible) continue;
                el.style.top = y;
                y += RowHeight + RowGap;
            }
            if (_toastLane != null)
                _toastLane.style.top = y + 12;
            _container?.MarkDirtyRepaint();
        }

        public void Show(RowId id) { SetVisible(id, true); }
        public void Hide(RowId id) { SetVisible(id, false); }

        private void SetVisible(RowId id, bool visible)
        {
            if (!_rows.TryGetValue(id, out var el)) return;
            el.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            Layout();
        }
    }
}
