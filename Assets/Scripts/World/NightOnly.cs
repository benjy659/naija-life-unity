// M2.2: shows this object's renderers only at night (TimeOfDay.Night01 above
// the threshold), e.g. the skyline's lit windows. With no TimeOfDay in the
// scene it stays hidden, which reads as day.

using UnityEngine;

namespace NaijaLife.World
{
    public class NightOnly : MonoBehaviour
    {
        public float nightThreshold = 0.5f;
        public float refreshSeconds = 0.5f;

        Renderer[] _renderers;
        float _next;
        bool _shown = true;

        void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            Show(false);
        }

        void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + refreshSeconds;
            var tod = TimeOfDay.Instance;
            Show(tod != null && tod.Night01 > nightThreshold);
        }

        void Show(bool on)
        {
            if (on == _shown) return;
            _shown = on;
            foreach (var r in _renderers) if (r != null) r.enabled = on;
        }
    }
}
