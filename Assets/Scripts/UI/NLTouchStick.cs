// Naija Streets (Unity rebuild) — NLTouchStick
// Floating stick. Spec section 5.2. The stick zone is the bottom-left
// quarter: left 50% of the width, bottom 45% of the height (portrait) or
// left 40%, bottom 70% (landscape). A touch anywhere in the zone moves the
// stick base under the thumb; release returns it to its rest place over
// 0.22 s. Radius 52 px of thumb travel. Dead zone 12%. Output 0..1
// magnitude into the Move action (<Gamepad>/leftStick through
// NLStickControl), so a light tilt walks and a full tilt jogs (M1.1
// behaviour unchanged). Two thumbs at once must work: stick plus look.

using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace NaijaLife.UI
{
    public class NLTouchStick
    {
        private const float ThumbTravel = 52f;
        private const float DeadZone = 0.12f;
        private const float ReturnSeconds = 0.22f;

        private readonly VisualElement _zone;
        private readonly VisualElement _base;
        private readonly VisualElement _thumb;
        private readonly NLStickControl _control;
        private readonly MonoBehaviour _host;

        private int _pointerId = -1;
        private Vector2 _baseCenter;   // zone-local px of the base centre
        private Vector2 _restCenter;   // zone-local px of the rest place
        private Coroutine _returnRoutine;

        public Vector2 Output { get; private set; }
        public bool IsActive => _pointerId >= 0;

        public NLTouchStick(VisualElement zone, VisualElement stickBase, VisualElement thumb,
            NLStickControl control, MonoBehaviour host)
        {
            _zone = zone;
            _base = stickBase;
            _thumb = thumb;
            _control = control;
            _host = host;

            _zone.RegisterCallback<PointerDownEvent>(OnPointerDown);
            _zone.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            _zone.RegisterCallback<PointerUpEvent>(OnPointerUp);
            _zone.RegisterCallback<PointerCancelEvent>(OnPointerCancel);
            _zone.RegisterCallback<GeometryChangedEvent>(_ => CacheRestPlace());
        }

        public void SetRestOffset(Vector2 offsetPx)
        {
            // Called by the screen guard when the player keep-out box touches
            // the stick's rest place: rest 24 px further left and down.
            _restCenter += offsetPx;
            if (!IsActive) PlaceBase(_restCenter);
        }

        private void CacheRestPlace()
        {
            // Rest place: 12 px from the bottom-left of the zone (spec 5.1).
            float baseHalf = 62f;
            _restCenter = new Vector2(baseHalf + 12f, _zone.layout.height - baseHalf - 12f);
            if (!IsActive) PlaceBase(_restCenter);
        }

        private void OnPointerDown(PointerDownEvent e)
        {
            if (_pointerId >= 0) return;
            if (e.pointerType == UnityEngine.UIElements.PointerType.mouse) return; // mouse drags look, it never drives the stick
            _pointerId = e.pointerId;
            _zone.CapturePointer(e.pointerId);
            if (_returnRoutine != null) { _host.StopCoroutine(_returnRoutine); _returnRoutine = null; }
            Vector2 local = _zone.WorldToLocal(e.position);
            _baseCenter = local;
            PlaceBase(_baseCenter);
            UpdateOutput(local);
            e.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent e)
        {
            if (e.pointerId != _pointerId) return;
            UpdateOutput(_zone.WorldToLocal(e.position));
            e.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent e)
        {
            if (e.pointerId != _pointerId) return;
            EndPointer(e.pointerId);
            e.StopPropagation();
        }

        private void OnPointerCancel(PointerCancelEvent e)
        {
            if (e.pointerId != _pointerId) return;
            EndPointer(e.pointerId);
            e.StopPropagation();
        }

        private void EndPointer(int pointerId)
        {
            _pointerId = -1;
            _zone.ReleasePointer(pointerId);
            Output = Vector2.zero;
            _control?.SetValue(Vector2.zero);
            _returnRoutine = _host.StartCoroutine(ReturnBase());
        }

        private void UpdateOutput(Vector2 pointerLocal)
        {
            Vector2 delta = pointerLocal - _baseCenter;
            float mag = delta.magnitude;
            if (mag > ThumbTravel)
            {
                delta = delta.normalized * ThumbTravel;
                mag = ThumbTravel;
            }
            // Thumb visual follows the pointer (clamped).
            _thumb.style.left = 62f - 26f + delta.x;
            _thumb.style.top = 62f - 26f + delta.y;

            float norm = mag / ThumbTravel;
            Vector2 output = Vector2.zero;
            if (norm > DeadZone)
            {
                // Rescale so output runs 0..1 from the dead zone edge.
                float scaled = (norm - DeadZone) / (1f - DeadZone);
                output = delta.normalized * scaled;
                output.y = -output.y; // UI Toolkit Y is down; Move Y is forward
            }
            Output = output;
            _control?.SetValue(output);
        }

        private void PlaceBase(Vector2 centre)
        {
            _base.style.left = centre.x - 62f;
            _base.style.top = centre.y - 62f;
            _thumb.style.left = 62f - 26f;
            _thumb.style.top = 62f - 26f;
        }

        private IEnumerator ReturnBase()
        {
            Vector2 from = _baseCenter;
            float t = 0f;
            while (t < ReturnSeconds)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / ReturnSeconds);
                PlaceBase(Vector2.Lerp(from, _restCenter, k));
                yield return null;
            }
            PlaceBase(_restCenter);
            _returnRoutine = null;
        }
    }
}
