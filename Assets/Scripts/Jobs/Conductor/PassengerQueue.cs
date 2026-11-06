// Naija Streets (Unity) - waiting passengers at a bay (greybox capsules until
// character art). Up to 6 wait on the island behind aside-west, 0.8 m apart.
// A called passenger walks to the door and boards (inside the bus = hidden).
// At the drop stop passengers walk off and are removed once 12 m away.
// Phone budget: at most 12 skinned NPCs on screen (design inventory section 2).

using System.Collections.Generic;
using UnityEngine;

namespace NaijaLife.JobsGlue
{
    public sealed class PassengerQueue : MonoBehaviour
    {
        public int maxWaiting = 6;
        public float spacing = 0.8f;
        [Tooltip("Direction the queue extends from this transform.")]
        public Vector3 queueDirection = Vector3.left;
        public Transform door;
        public GameObject passengerPrefab;

        const float WalkSpeed = 1.5f;
        readonly List<Transform> _waiting = new List<Transform>();
        readonly List<(Transform t, Vector3 to, bool board)> _walking = new List<(Transform, Vector3, bool)>();
        float _refillAt;

        public int Waiting => _waiting.Count;
        public int Boarded { get; private set; }

        void Update()
        {
            if (_waiting.Count < maxWaiting && Time.time >= _refillAt) { Spawn(); _refillAt = Time.time + 1.2f; }
            for (int i = _waiting.Count - 1; i >= 0; i--) StepToward(_waiting[i], SpotPos(i), 1.0f);
            for (int i = _walking.Count - 1; i >= 0; i--)
            {
                var w = _walking[i];
                if (!w.t) { _walking.RemoveAt(i); continue; }
                if (StepToward(w.t, w.to, WalkSpeed))
                {
                    _walking.RemoveAt(i);
                    if (w.board) { w.t.gameObject.SetActive(false); Destroy(w.t.gameObject, 0.1f); }
                    else Destroy(w.t.gameObject);
                }
            }
        }

        /// <summary>Call the front passenger to the door. False if nobody is waiting.</summary>
        public bool CallOne()
        {
            if (_waiting.Count == 0 || !door) return false;
            Transform p = _waiting[0];
            _waiting.RemoveAt(0);
            _walking.Add((p, door.position, true));
            Boarded++;
            return true;
        }

        /// <summary>Passengers get down at the stop and walk away along the pavement.</summary>
        public void Alight(Vector3 doorPos, Vector3 awayDir, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Transform p = Make(doorPos + awayDir.normalized * 0.4f * i);
                _walking.Add((p, doorPos + awayDir.normalized * (12f + i), false));
            }
            Boarded = 0;
        }

        public void ResetBoarded() { Boarded = 0; }

        void Spawn()
        {
            Transform p = Make(SpotPos(maxWaiting) + queueDirection.normalized * 2f);
            _waiting.Add(p);
        }

        Transform Make(Vector3 at)
        {
            GameObject g = passengerPrefab ? Instantiate(passengerPrefab) : GameObject.CreatePrimitive(PrimitiveType.Capsule);
            if (!passengerPrefab)
            {
                g.transform.localScale = new Vector3(0.45f, 0.85f, 0.45f);
                Collider c = g.GetComponent<Collider>(); if (c) Destroy(c);
            }
            g.name = "Passenger";
            g.transform.position = at + Vector3.up * 0.85f;
            return g.transform;
        }

        Vector3 SpotPos(int i) => transform.position + queueDirection.normalized * spacing * i;

        bool StepToward(Transform t, Vector3 to, float speed)
        {
            Vector3 target = new Vector3(to.x, t.position.y, to.z);
            Vector3 d = target - t.position;
            if (d.sqrMagnitude < 0.0025f) return true;
            t.position = Vector3.MoveTowards(t.position, target, speed * Time.deltaTime);
            if (d.sqrMagnitude > 0.0001f) t.rotation = Quaternion.LookRotation(new Vector3(d.x, 0f, d.z));
            return false;
        }
    }
}
