// Edit Mode tests for the M3 pure logic (docs/specs/vehicles-m3.md).
// Ted's team: Window > General > Test Runner > EditMode > Run All.
// These also compiled and passed outside Unity with mono (Claude, 2026-11-06).

using NUnit.Framework;

namespace NaijaLife.Vehicles.Tests
{
    public class VehicleLogicTests
    {
        const float Dt = 0.02f; // 50 Hz physics (spec 6)

        // ---- Torque and steering ----

        [Test]
        public void Torque_Full_To_60_Percent_Then_Falls_To_Zero()
        {
            Assert.AreEqual(1f, VehicleMath.TorqueFactor(0f, 25f), 1e-5f);
            Assert.AreEqual(1f, VehicleMath.TorqueFactor(15f, 25f), 1e-5f);
            Assert.AreEqual(0.5f, VehicleMath.TorqueFactor(20f, 25f), 1e-5f);
            Assert.AreEqual(0f, VehicleMath.TorqueFactor(25f, 25f), 1e-5f);
            Assert.AreEqual(0f, VehicleMath.TorqueFactor(30f, 25f), 1e-5f);
            Assert.AreEqual(1f, VehicleMath.TorqueFactor(-3f, 25f), 1e-5f, "reverse uses |speed|");
        }

        [Test]
        public void Steer_Limit_Narrows_With_Speed()
        {
            Assert.AreEqual(38f, VehicleMath.MaxSteerAngle(0f, 25f, 38f, 10f), 1e-4f);
            Assert.AreEqual(24f, VehicleMath.MaxSteerAngle(12.5f, 25f, 38f, 10f), 1e-4f);
            Assert.AreEqual(10f, VehicleMath.MaxSteerAngle(40f, 25f, 38f, 10f), 1e-4f);
        }

        [Test]
        public void Danfo_Torque_For_7s_To_50()
        {
            // 1800 kg, 0.33 m wheel: a = 13.89/7 = 1.98 m/s2, F = 3571 N, T = 1179 N m.
            Assert.AreEqual(1178.6f, VehicleMath.TorqueFor0To50(1800f, 7f, 0.33f), 1f);
        }

        // ---- Traffic gap rules ----

        [Test]
        public void SpeedForGap_Stops_A_Car_Length_Short()
        {
            Assert.AreEqual(0f, VehicleMath.SpeedForGap(4.5f, 14f));
            Assert.AreEqual(0f, VehicleMath.SpeedForGap(2f, 14f));
            Assert.AreEqual(14f, VehicleMath.SpeedForGap(100f, 14f), 1e-4f);
            // 10 m of room at 5 m/s2: v = 10 m/s.
            Assert.AreEqual(10f, VehicleMath.SpeedForGap(14.5f, 14f), 1e-4f);
        }

        [Test]
        public void SpeedForGap_Brakes_In_Time_From_Lane_Speed()
        {
            // Simulate a bot at 14 m/s closing on a stopped car 60 m ahead, braking
            // at up to 5 m/s2 toward SpeedForGap. It must stop >= 4 m short, never touch.
            float v = 14f, gap = 60f;
            for (int i = 0; i < 2000 && (v > 0.01f || gap > 4.5f); i++)
            {
                float target = VehicleMath.SpeedForGap(gap, 14f);
                v = VehicleMath.MoveTowards(v, target, (target < v ? 5f : 2f) * Dt);
                gap -= v * Dt;
                Assert.Greater(gap, 4f, "closed inside the standoff at step " + i);
            }
            Assert.Less(v, 0.05f);
        }

        [Test]
        public void Follow_Keeps_Two_Second_Gap()
        {
            float leader = 10f, v = 14f, gap = 15f;
            for (int i = 0; i < 3000; i++)
            {
                float target = VehicleMath.SpeedForFollow(gap, leader, 14f);
                v = VehicleMath.MoveTowards(v, target, 4f * Dt);
                gap += (leader - v) * Dt;
                Assert.Greater(gap, 4.5f);
            }
            Assert.AreEqual(leader, v, 0.2f);
            Assert.AreEqual(4.5f + leader * 2f, gap, 1.0f);
        }

        [Test]
        public void Crossing_First_Arrival_Goes_Tie_Lower_Id()
        {
            Assert.IsTrue(VehicleMath.HasRightOfWay(1.0f, 7, 2.0f, 3));
            Assert.IsFalse(VehicleMath.HasRightOfWay(2.0f, 3, 1.0f, 7));
            Assert.IsTrue(VehicleMath.HasRightOfWay(1.0f, 3, 1.1f, 7));
            Assert.IsFalse(VehicleMath.HasRightOfWay(1.1f, 7, 1.0f, 3));
            // Exactly one of two tied vehicles goes.
            Assert.AreNotEqual(VehicleMath.HasRightOfWay(1f, 4, 1f, 5), VehicleMath.HasRightOfWay(1f, 5, 1f, 4));
        }

        // ---- Exit ----

        [Test]
        public void Exit_Only_Below_2ms_And_First_Free_Side()
        {
            Assert.IsTrue(VehicleMath.CanExit(0f));
            Assert.IsTrue(VehicleMath.CanExit(-1.9f));
            Assert.IsFalse(VehicleMath.CanExit(2.5f));
            Assert.AreEqual(0, VehicleMath.FirstFreeExit(new[] { false, false, false }));
            Assert.AreEqual(1, VehicleMath.FirstFreeExit(new[] { true, false, false }), "wall on driver side: passenger side");
            Assert.AreEqual(2, VehicleMath.FirstFreeExit(new[] { true, true, false }));
            Assert.AreEqual(-1, VehicleMath.FirstFreeExit(new[] { true, true, true }), "boxed in: stay seated");
        }

        // ---- Pedals ----

        [Test]
        public void Pedals_Brake_Then_Reverse_After_Hold()
        {
            var p = new PedalLogic();
            p.Step(0f, 1f, 5f, Dt, out float m, out float b);
            Assert.AreEqual(0f, m); Assert.AreEqual(1f, b); Assert.IsFalse(p.InReverse);

            // Stopped, brake held: brakes for under 0.25 s, then reverse.
            int steps = 0;
            while (!p.InReverse && steps < 100) { p.Step(0f, 1f, 0f, Dt, out m, out b); steps++; }
            Assert.IsTrue(p.InReverse);
            Assert.AreEqual(13, steps, 1, "about 0.25 s at 50 Hz");
            p.Step(0f, 1f, -0.5f, Dt, out m, out b);
            Assert.Less(m, 0f, "brake pedal now drives backwards");
        }

        [Test]
        public void Pedals_Gas_While_Reversing_Brakes_Then_Drives()
        {
            var p = new PedalLogic();
            for (int i = 0; i < 20; i++) p.Step(0f, 1f, 0f, Dt, out _, out _);
            Assert.IsTrue(p.InReverse);
            p.Step(1f, 0f, -3f, Dt, out float m, out float b);
            Assert.AreEqual(0f, m); Assert.AreEqual(1f, b);
            p.Step(1f, 0f, -0.1f, Dt, out m, out b);
            Assert.IsFalse(p.InReverse); Assert.AreEqual(1f, m);
        }

        [Test]
        public void Pedals_Gas_While_Rolling_Back_In_Drive_Brakes_First()
        {
            var p = new PedalLogic();
            p.Step(1f, 0f, -2f, Dt, out float m, out float b);
            Assert.AreEqual(0f, m); Assert.AreEqual(1f, b);
        }

        // ---- Stuck recovery: never drive through ----

        [Test]
        public void Stuck_Overtakes_A_Parked_Car_When_Clear()
        {
            var s = new StuckLogic();
            for (int i = 0; i < 99; i++) s.Step(5f, false, true, Dt);
            Assert.AreEqual(StuckState.Driving, s.State, "under 2 s");
            s.Step(5f, false, true, Dt);
            Assert.AreEqual(StuckState.TryOvertake, s.State);
        }

        [Test]
        public void Stuck_Queue_Behind_Moving_Car_Is_Not_Stuck()
        {
            var s = new StuckLogic();
            for (int i = 0; i < 1000; i++) s.Step(5f, true, true, Dt);
            Assert.AreEqual(StuckState.Driving, s.State);
        }

        [Test]
        public void Stuck_No_Way_Round_Waits_Reverses_Then_Reroutes()
        {
            var s = new StuckLogic();
            for (int attempt = 1; attempt <= StuckLogic.MaxTries; attempt++)
            {
                int guard = 0;
                while (s.State != StuckState.Reversing && s.State != StuckState.Reroute && guard++ < 5000)
                    s.Step(3f, false, false, Dt);
                if (attempt < StuckLogic.MaxTries)
                {
                    Assert.AreEqual(StuckState.Reversing, s.State, "attempt " + attempt);
                    guard = 0;
                    while (s.State == StuckState.Reversing && guard++ < 5000) s.Step(3f, false, false, Dt, 0.05f);
                    Assert.AreEqual(StuckState.Driving, s.State);
                }
            }
            Assert.AreEqual(StuckState.Reroute, s.State);
            Assert.AreEqual(StuckLogic.MaxTries, s.Tries);
        }

        // ---- Damage, input ----

        [Test]
        public void Damage_Ignores_Small_Bumps()
        {
            Assert.AreEqual(0f, VehicleMath.DamageFromImpulse(500f, 1500f, 400f));
            Assert.AreEqual(10f, VehicleMath.DamageFromImpulse(4000f, 1500f, 400f), 1e-4f);
        }

        [Test]
        public void DriveInput_Clamps()
        {
            var d = new DriveInput { Throttle = 2f, Brake = -1f, Steer = -3f }.Clamped();
            Assert.AreEqual(1f, d.Throttle); Assert.AreEqual(0f, d.Brake); Assert.AreEqual(-1f, d.Steer);
        }
    }
}
