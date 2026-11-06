// Naija Streets (Unity) - player needs. M4 has Pawa only (docs/specs/jobs-m4.md
// section 7): 0 to 100, 25 to clock in, drains 1.2 a minute while working.
// Food and sleep refill it from M6; the dev keys below are for testing only.

using NaijaLife.Jobs;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NaijaLife.Player
{
    public sealed class PlayerNeeds : MonoBehaviour
    {
        [Range(0, 100)] [SerializeField] float startPawa = 100f;
        public PawaMeter Pawa { get; private set; }

        void Awake() { Pawa = new PawaMeter(startPawa); }

        void Update()
        {
#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && ENABLE_INPUT_SYSTEM
            // Dev only: F6 sets Pawa to 20 (gate test), F7 fills it.
            Keyboard k = Keyboard.current;
            if (k != null && k.f6Key.wasPressedThisFrame) Pawa.Set(20f);
            if (k != null && k.f7Key.wasPressedThisFrame) Pawa.Set(100f);
#endif
        }
    }
}
