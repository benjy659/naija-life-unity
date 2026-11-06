// M3 test-scene overlay: speed, gear, health, last contacts, bot count. For the
// qa/m3.* screenshots (spec section 11). Plain OnGUI on purpose: the real HUD
// (speedo bottom-right, prompt) is the HUD thread's job. Toggle with F3.

using UnityEngine;
using UnityEngine.InputSystem;

namespace NaijaLife.Vehicles
{
    public sealed class VehicleDebugOverlay : MonoBehaviour
    {
        public DriveSession session;
        public TrafficSpawner spawner;
        public bool visible = true;

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame) visible = !visible;
            if (session == null) session = FindFirstObjectByType<DriveSession>();
            if (spawner == null) spawner = FindFirstObjectByType<TrafficSpawner>();
        }

        void OnGUI()
        {
            if (!visible) return;
            var sb = new System.Text.StringBuilder();
            if (session != null && session.CurrentVehicle != null)
            {
                var v = session.CurrentVehicle;
                var dmg = v.GetComponent<VehicleDamage>();
                sb.AppendLine($"{v.SpeedKmh:0} km/h  {(v.Pedals.InReverse ? "R" : "D")}  health {(dmg != null ? dmg.health : 100f):0}");
                var rep = v.GetComponent<VehicleContactReporter>();
                if (rep != null)
                    foreach (var c in rep.Recent)
                        sb.AppendLine($"hit {c.Other?.name} {(c.IsPerson ? "PERSON " : "")}{c.ImpactKmh:0} km/h");
            }
            else if (session != null)
            {
                sb.AppendLine(session.State.ToString());
            }
            if (spawner != null) sb.AppendLine($"bots {spawner.AliveCount}");
            GUI.Label(new Rect(10, 10, 420, 220), sb.ToString());
        }
    }
}
