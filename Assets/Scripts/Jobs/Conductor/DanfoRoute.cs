// Naija Streets (Unity) - the bay danfo's route (docs/specs/jobs-m4.md section 6.4).
// Waypoints are this object's children in order: bay, ..., drop stop, ...,
// turning point, ..., back to bay. Mark the drop stop child by name "Stop".
// The bus is a kinematic follower (8 to 10 m/s, eases in and out of stops,
// no traffic AI in M4). Oshodi layout: out-and-back to the lay-by at z -646
// with the roundabout at z -720 as the turning point.

using System;
using System.Collections;
using UnityEngine;

namespace NaijaLife.JobsGlue
{
    public sealed class DanfoRoute : MonoBehaviour
    {
        public float cruiseSpeed = 9f;
        public float stopSeconds = 15f;
        [Tooltip("Metres over which the bus speeds up from and slows into a stop.")]
        public float easeMetres = 25f;

        public event Action ReachedStop;
        public event Action ReachedBay;
        public bool Running { get; private set; }

        public int StopIndex
        {
            get
            {
                for (int i = 0; i < transform.childCount; i++) if (transform.GetChild(i).name.StartsWith("Stop")) return i;
                return transform.childCount / 2;
            }
        }

        /// <summary>Drive the bus once round the route: bay -> stop (wait) -> back to bay.</summary>
        public IEnumerator Drive(Transform bus, Func<bool> holdAtStop = null)
        {
            if (transform.childCount < 2 || !bus) yield break;
            Running = true;
            int stop = StopIndex;
            yield return Leg(bus, 0, stop);
            ReachedStop?.Invoke();
            float until = Time.time + stopSeconds;
            while (Time.time < until || (holdAtStop != null && holdAtStop())) yield return null;
            yield return Leg(bus, stop, transform.childCount - 1);
            Running = false;
            ReachedBay?.Invoke();
        }

        IEnumerator Leg(Transform bus, int from, int to)
        {
            float total = 0f;
            for (int i = from; i < to; i++) total += Vector3.Distance(Pt(i), Pt(i + 1));
            float done = 0f;
            for (int i = from; i < to; i++)
            {
                Vector3 a = Pt(i), b = Pt(i + 1);
                float len = Vector3.Distance(a, b), t = 0f;
                while (t < len)
                {
                    float fromStart = done + t, toEnd = total - fromStart;
                    float ease = Mathf.Clamp01(Mathf.Min(fromStart, toEnd) / Mathf.Max(1f, easeMetres));
                    float v = Mathf.Lerp(1.5f, cruiseSpeed, ease);
                    t = Mathf.Min(len, t + v * Time.deltaTime);
                    Vector3 p = Vector3.Lerp(a, b, len > 0f ? t / len : 1f);
                    Vector3 dir = b - a; dir.y = 0f;
                    bus.position = p;
                    if (dir.sqrMagnitude > 0.001f)
                        bus.rotation = Quaternion.RotateTowards(bus.rotation, Quaternion.LookRotation(dir), 60f * Time.deltaTime);
                    yield return null;
                }
                done += len;
            }
        }

        Vector3 Pt(int i) => transform.GetChild(i).position;

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.1f);
            for (int i = 0; i + 1 < transform.childCount; i++) Gizmos.DrawLine(Pt(i), Pt(i + 1));
        }
    }
}
