// M3 contact events (docs/specs/vehicles-m3.md section 10). Raised ONLY from real
// collisions (OnCollisionEnter), never from a distance check or a radius: web
// lesson L-123 and the B-39 rules (a phantom-hit radius once jailed the owner
// for hitting nobody). One event per other body per 1.5 s.
//
// M3 only logs these. The police work will subscribe to AnyContact later and
// apply the B-39 rules: a hit on a person is contact at >= 15 km/h; a near miss
// is never a crime; children never count.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace NaijaLife.Vehicles
{
    public struct VehicleContact
    {
        public Collider Other;
        public bool IsPerson;
        public VehicleController OtherVehicle;
        /// <summary>Relative impact speed (km/h).</summary>
        public float ImpactKmh;
        /// <summary>Collision impulse magnitude (N s).</summary>
        public float Impulse;
        public Vector3 Point;
        public float Time;
    }

    [RequireComponent(typeof(VehicleController))]
    [DisallowMultipleComponent]
    public sealed class VehicleContactReporter : MonoBehaviour
    {
        public const float RepeatWindowSeconds = 1.5f;

        public event Action<VehicleContact> OnContact;
        /// <summary>Every vehicle's contacts, for systems that listen once (police, damage HUD).</summary>
        public static event Action<VehicleController, VehicleContact> AnyContact;

        public bool logContacts = true;

        VehicleController _v;
        readonly Dictionary<int, float> _lastByBody = new Dictionary<int, float>();
        readonly List<int> _expired = new List<int>();

        /// <summary>Last few contacts, newest first, for the debug overlay.</summary>
        public readonly List<VehicleContact> Recent = new List<VehicleContact>(8);

        void Awake() { _v = GetComponent<VehicleController>(); }

        void OnCollisionEnter(Collision c)
        {
            var other = c.collider;
            if (other == null) return;
            // Skip the road and floors: only bodies and walls count.
            if (other.gameObject.layer == VehicleLayers.Ground && Vector3.Dot(c.GetContact(0).normal, Vector3.up) > 0.7f) return;

            int key = other.attachedRigidbody != null ? other.attachedRigidbody.GetInstanceID() : other.GetInstanceID();
            float now = Time.time;
            if (_lastByBody.TryGetValue(key, out float last) && now - last < RepeatWindowSeconds) return;
            _lastByBody[key] = now;
            Prune(now);

            var contact = new VehicleContact
            {
                Other = other,
                IsPerson = VehicleLayers.IsPersonLayer(other.gameObject.layer) || other.CompareTag("Player"),
                OtherVehicle = TrafficSense.VehicleOf(other),
                ImpactKmh = c.relativeVelocity.magnitude * VehicleMath.KmhPerMs,
                Impulse = c.impulse.magnitude,
                Point = c.contactCount > 0 ? c.GetContact(0).point : transform.position,
                Time = now
            };

            Recent.Insert(0, contact);
            if (Recent.Count > 8) Recent.RemoveAt(Recent.Count - 1);
            if (logContacts)
                Debug.Log($"[VehicleContact] {name} hit {other.name} person={contact.IsPerson} " +
                          $"{contact.ImpactKmh:0} km/h impulse={contact.Impulse:0}", this);
            OnContact?.Invoke(contact);
            AnyContact?.Invoke(_v, contact);
        }

        void Prune(float now)
        {
            if (_lastByBody.Count < 32) return;
            _expired.Clear();
            foreach (var kv in _lastByBody) if (now - kv.Value > RepeatWindowSeconds) _expired.Add(kv.Key);
            foreach (var k in _expired) _lastByBody.Remove(k);
        }
    }
}
