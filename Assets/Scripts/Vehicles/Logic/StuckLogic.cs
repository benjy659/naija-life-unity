// M3 bot "never drive through" recovery (docs/specs/vehicles-m3.md section 8.6).
// Pure state machine; TrafficDriver feeds it the sensed gap each step and acts
// on the returned state. A bot NEVER turns off its collider or passes through.
//
//   Driving --(gap < 6 m, blocker still, 2 s)--> TryOvertake (if the offset path is clear)
//                                            \-> Waiting (if not)
//   Waiting --(12 s)--> Reversing (up to 10 m) --> Driving (try again)
//   after 3 failed tries --> Reroute (pick another route, or despawn when out of view)

namespace NaijaLife.Vehicles
{
    public enum StuckState { Driving, TryOvertake, Waiting, Reversing, Reroute }

    public sealed class StuckLogic
    {
        public const float BlockGap = 6f;
        public const float BlockSeconds = 2f;
        public const float WaitSeconds = 12f;
        public const float ReverseMetres = 10f;
        public const int MaxTries = 3;
        public const float OvertakeMaxSeconds = 8f;
        const float Eps = 1e-4f; // float timers summed from dt

        public StuckState State { get; private set; } = StuckState.Driving;
        public int Tries { get; private set; }

        float _timer;
        float _reversed;

        /// <summary>
        /// gap: metres to the nearest thing ahead (float.PositiveInfinity when clear).
        /// blockerMoving: the thing ahead moves (another car in a queue is not "stuck").
        /// offsetClear: the overtaking offset path is clear (TrafficSense cast).
        /// reverseStep: metres reversed this step (only used while Reversing).
        /// </summary>
        public StuckState Step(float gap, bool blockerMoving, bool offsetClear, float dt, float reverseStep = 0f)
        {
            switch (State)
            {
                case StuckState.Driving:
                    if (gap < BlockGap && !blockerMoving)
                    {
                        _timer += dt;
                        if (_timer >= BlockSeconds - Eps)
                        {
                            _timer = 0f;
                            State = offsetClear ? StuckState.TryOvertake : StuckState.Waiting;
                        }
                    }
                    else
                    {
                        _timer = 0f;
                    }
                    break;

                case StuckState.TryOvertake:
                    _timer += dt;
                    if (gap >= BlockGap * 2f && _timer > 1f)
                    {
                        // Passed it: back to the lane, fresh start.
                        _timer = 0f;
                        Tries = 0;
                        State = StuckState.Driving;
                    }
                    else if (!offsetClear || _timer > OvertakeMaxSeconds)
                    {
                        _timer = 0f;
                        State = StuckState.Waiting;
                    }
                    break;

                case StuckState.Waiting:
                    if (gap >= BlockGap * 2f || blockerMoving)
                    {
                        _timer = 0f;
                        State = StuckState.Driving;
                        break;
                    }
                    _timer += dt;
                    if (offsetClear)
                    {
                        _timer = 0f;
                        State = StuckState.TryOvertake;
                    }
                    else if (_timer >= WaitSeconds - Eps)
                    {
                        _timer = 0f;
                        _reversed = 0f;
                        Tries++;
                        State = Tries >= MaxTries ? StuckState.Reroute : StuckState.Reversing;
                    }
                    break;

                case StuckState.Reversing:
                    _reversed += reverseStep < 0f ? -reverseStep : reverseStep;
                    _timer += dt;
                    if (_reversed >= ReverseMetres || _timer > 8f)
                    {
                        _timer = 0f;
                        State = StuckState.Driving;
                    }
                    break;

                case StuckState.Reroute:
                    break; // TrafficDriver picks a new route, then calls Reset().
            }
            return State;
        }

        public void Reset()
        {
            State = StuckState.Driving;
            Tries = 0;
            _timer = 0f;
            _reversed = 0f;
        }
    }
}
