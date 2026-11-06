// M3 one-pedal-pair logic (docs/specs/vehicles-m3.md section 6), like GTA:
//   brake while rolling forward   -> brakes
//   brake held below 1 km/h 0.25 s -> reverse (brake pedal becomes reverse throttle)
//   throttle while rolling back    -> brakes first, then drives forward
// Pure logic, no UnityEngine.

using System;

namespace NaijaLife.Vehicles
{
    public sealed class PedalLogic
    {
        public const float StopSpeedMs = 1f / 3.6f;   // 1 km/h
        public const float ReverseHoldSeconds = 0.25f;

        /// <summary>True while the gearbox is in reverse.</summary>
        public bool InReverse { get; private set; }

        float _heldAtStop;

        /// <summary>
        /// One physics step. forwardSpeed is signed (m/s, + = nose first).
        /// motor: -1..1 share of motor torque (negative = reverse).
        /// brake: 0..1 share of brake torque.
        /// </summary>
        public void Step(float throttle, float brake, float forwardSpeed, float dt, out float motor, out float brakeOut)
        {
            throttle = VehicleMath.Clamp01(throttle);
            brake = VehicleMath.Clamp01(brake);
            motor = 0f;
            brakeOut = 0f;
            bool stopped = Math.Abs(forwardSpeed) < StopSpeedMs;

            if (!InReverse)
            {
                if (brake > 0.05f)
                {
                    if (stopped && throttle < 0.05f)
                    {
                        _heldAtStop += dt;
                        if (_heldAtStop >= ReverseHoldSeconds - 1e-4f)
                        {
                            InReverse = true;
                            _heldAtStop = 0f;
                        }
                        else
                        {
                            brakeOut = brake;   // hold still until reverse engages
                        }
                    }
                    else
                    {
                        _heldAtStop = 0f;
                        brakeOut = brake;
                    }
                }
                else
                {
                    _heldAtStop = 0f;
                }

                if (!InReverse)
                {
                    if (forwardSpeed < -StopSpeedMs && throttle > 0.05f)
                        brakeOut = Math.Max(brakeOut, throttle); // rolling back: gas brakes first
                    else
                        motor = throttle;
                    return;
                }
            }

            // In reverse: brake pedal drives backwards, gas brakes then returns to drive.
            if (throttle > 0.05f)
            {
                if (forwardSpeed < -StopSpeedMs)
                {
                    brakeOut = throttle;
                    return;
                }
                InReverse = false;
                _heldAtStop = 0f;
                motor = throttle;
                return;
            }
            motor = -brake;
        }

        public void Reset()
        {
            InReverse = false;
            _heldAtStop = 0f;
        }
    }
}
