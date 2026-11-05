// Naija Streets (Unity) — feeds the local player's pose to the network layer
// every frame. The layer decides when to send (2 Hz walking, 4 Hz driving).
// Put on the Player prefab next to PlayerController.
// UNVERIFIED (2026-11-05): not compiled or run yet.

using NaijaLife.Network;
using NaijaLife.Player;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NaijaLife.NetworkGlue
{
    [RequireComponent(typeof(PlayerController))]
    public sealed class NetPlayerBridge : MonoBehaviour
    {
        const float MovingSpeed = 0.1f;  // m/s
        const float RunningSpeed = 4.6f; // above jog 4.2 (design inventory §2)

        PlayerController _pc;

        /// <summary>Set by the vehicle system (M3) while driving; 0 = on foot.</summary>
        public ushort VehicleModelId { get; set; }
        public bool Indoors { get; set; }

        void Awake() => _pc = GetComponent<PlayerController>();

        void Update()
        {
            INetSession s = NetBootstrap.Session;
            if (s == null) return;

            Vector3 p = transform.position;
            float speed = _pc.CurrentSpeed;
            MoveFlags f = MoveFlags.None;
            if (speed > MovingSpeed) f |= MoveFlags.Moving;
            if (speed > RunningSpeed) f |= MoveFlags.Running;
            if (VehicleModelId != 0) f |= MoveFlags.Driving;
            if (Indoors) f |= MoveFlags.Indoors;

            s.Players.SubmitLocal(new NetPose
            {
                X = p.x, Y = p.y, Z = p.z,
                HeadingDeg = transform.eulerAngles.y,
                Speed = speed,
                Flags = f,
                AnimId = (byte)_pc.CurrentBand,
                VehicleModelId = VehicleModelId,
            }, Time.realtimeSinceStartupAsDouble);
            s.SetInterestCenter(p.x, p.z);

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && ENABLE_INPUT_SYSTEM
            // Smoke-test key until emotes have a real button (M5.1 acceptance 5).
            if (Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame)
                s.Events.Raise(WorldEventKind.Wave, 0);
#endif
        }
    }
}
