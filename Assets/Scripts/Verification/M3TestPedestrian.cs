// Greybox_M3 test pedestrian (docs/specs/vehicles-m3.md sections 2, 8.4). A kinematic
// capsule on layer Pedestrian. The "steps out" one walks 4 m into the road every
// 25 s, stands 5 s, then walks back; if a bot honks (IStepAside) it goes back at once.
// Test-scene only: real pedestrians come with the crowd work.

using NaijaLife.Vehicles;
using UnityEngine;

namespace NaijaLife.Verification
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class M3TestPedestrian : MonoBehaviour, IStepAside
    {
        public bool stepsIntoRoad;
        public Vector3 stepDirection = Vector3.forward;
        public float stepDistance = 4f;
        public float period = 25f;
        public float standSeconds = 5f;
        public float walkSpeed = 1.3f;

        Rigidbody _rb;
        Vector3 _home;
        float _t;
        bool _goBack;
        public int StepAsideCount { get; private set; }

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.isKinematic = true;
            _home = transform.position;
            _t = period * 0.5f;
        }

        public void StepAside(VehicleController from)
        {
            _goBack = true;
            StepAsideCount++;
        }

        void FixedUpdate()
        {
            if (!stepsIntoRoad) return;
            _t += Time.fixedDeltaTime;
            float cycle = _t % period;
            float walk = stepDistance / walkSpeed;
            Vector3 target = _home;
            if (!_goBack && cycle < walk + standSeconds) target = _home + stepDirection.normalized * stepDistance;
            if (cycle >= walk + standSeconds) _goBack = false; // next cycle may step out again
            Vector3 p = Vector3.MoveTowards(_rb.position, target, walkSpeed * Time.fixedDeltaTime);
            _rb.MovePosition(p);
        }
    }
}
