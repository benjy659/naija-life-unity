// M3 damage stub (docs/specs/vehicles-m3.md section 10). Health 100. Each contact
// above the definition's impulse threshold removes impulse / damageScale. Smoke
// under 30. At 0 the engine stops until Repair(). No explosions, no wrecks.

using UnityEngine;

namespace NaijaLife.Vehicles
{
    [RequireComponent(typeof(VehicleController), typeof(VehicleContactReporter))]
    [DisallowMultipleComponent]
    public sealed class VehicleDamage : MonoBehaviour
    {
        public float health = 100f;
        [Tooltip("Optional smoke particle system, played under 30 health.")]
        public ParticleSystem smoke;

        VehicleController _v;
        VehicleContactReporter _contacts;

        void Awake()
        {
            _v = GetComponent<VehicleController>();
            _contacts = GetComponent<VehicleContactReporter>();
        }

        void OnEnable() { _contacts.OnContact += OnContact; }
        void OnDisable() { _contacts.OnContact -= OnContact; }

        void OnContact(VehicleContact c)
        {
            var def = _v.definition;
            if (def == null) return;
            float dmg = VehicleMath.DamageFromImpulse(c.Impulse, def.damageThreshold, def.damageScale);
            if (dmg <= 0f) return;
            health = Mathf.Max(0f, health - dmg);
            if (health <= 0f) _v.EngineOn = false;
            if (smoke != null && health < 30f && !smoke.isPlaying) smoke.Play();
        }

        public void Repair()
        {
            health = 100f;
            _v.EngineOn = true;
            if (smoke != null) smoke.Stop();
        }
    }
}
