// Naija Streets: base class for every piece of the screen UI.
// Waits until NLUIRoot has bound #nl-root, then calls OnBind once (again after
// the UIDocument rebuilds its tree). Subclasses use Tick instead of Update.
using UnityEngine;
using UnityEngine.UIElements;

namespace NaijaLife.UI
{
    public abstract class NLUIPart : MonoBehaviour
    {
        protected VisualElement Root { get; private set; }
        protected NLUIRoot UI => NLUIRoot.Instance;
        protected bool Bound => Root != null;

        protected abstract void OnBind(VisualElement root);
        protected virtual void OnUnbind() { }
        protected virtual void Tick() { }

        protected virtual void OnDisable()
        {
            if (Root != null) OnUnbind();
            Root = null;
        }

        protected virtual void Update()
        {
            var ui = NLUIRoot.Instance;
            if (ui == null || !ui.IsBound || ui.Root == null)
            {
                if (Root != null) { OnUnbind(); Root = null; }
                return;
            }
            if (Root != ui.Root)
            {
                if (Root != null) OnUnbind();
                Root = ui.Root;
                OnBind(Root);
            }
            Tick();
        }

        /// <summary>Show an element with a motion class next frame (so its USS transition plays).</summary>
        protected static void SlideIn(VisualElement e, string onClass = "is-on", string inClass = "is-in")
        {
            if (e == null) return;
            if (onClass != null) e.AddToClassList(onClass);
            e.schedule.Execute(() => e.AddToClassList(inClass)).StartingIn(16);
        }

        /// <summary>Play the exit transition, then drop the "on" class (display none) after delayMs.</summary>
        protected static void SlideOut(VisualElement e, long delayMs = 230, string onClass = "is-on", string inClass = "is-in")
        {
            if (e == null) return;
            e.RemoveFromClassList(inClass);
            if (onClass != null)
                e.schedule.Execute(() => { if (!e.ClassListContains(inClass)) e.RemoveFromClassList(onClass); })
                 .StartingIn(NLUIRoot.ReducedMotion ? 0 : delayMs);
        }
    }
}
