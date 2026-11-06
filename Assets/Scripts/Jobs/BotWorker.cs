// Naija Streets (Unity) - a bot worker that hands its job over gently
// (docs/specs/jobs-m4.md section 5). Working -> SteppingAside -> WaitingAside
// -> WalkingBack -> Working. Never vanishes, never teleports: every move is a
// walk at 1.3 m/s. Uses a NavMeshAgent when one is on the NavMesh, else walks
// in a straight line (the bay island is clear, so that is enough for M4).

using System;
using UnityEngine;
using UnityEngine.AI;

namespace NaijaLife.JobsGlue
{
    public sealed class BotWorker : MonoBehaviour
    {
        public enum State { Working, SteppingAside, WaitingAside, WalkingBack, Riding, WalkingToDoor }

        public const float WalkSpeed = 1.3f;
        const float ArriveDist = 0.15f;
        const float RetrySeconds = 2f;

        [Tooltip("Optional. Used when the bot stands on a baked NavMesh.")]
        public NavMeshAgent agent;
        [Tooltip("Optional. Gets float 'Speed' like the player's locomotion blend.")]
        public Animator animator;
        [Tooltip("What the bot faces while working or waiting (the bus).")]
        public Transform faceWhenIdle;

        public State Current { get; private set; } = State.Working;
        public static event Action<BotWorker, string> Barked;

        static readonly string[] WorkBarks = { "Yaba! Yaba!", "Ojuelegba straight!", "CMS! CMS!", "Ikeja along!", "Obalende! Obalende!", "Enter with your change!" };
        static readonly string[] AsideBarks = { "Shout am well!", "No dash dem change o.", "Oya, fill am!" };

        Transform _target;
        Action _onArrive;
        float _nextBark, _retryAt, _waitBefore;

        void Start() { _nextBark = Time.time + UnityEngine.Random.Range(2f, 6f); }

        /// <summary>Player took the job: one line, then walk to the aside point and wait.</summary>
        public void StepAside(Transform aside, bool byLocalPlayer)
        {
            if (byLocalPlayer) Bark("Oya, try am.");
            if (Current == State.Riding) Alight(transform.parent ? transform.parent.position : transform.position);
            WalkTo(aside, State.SteppingAside, () => { Current = State.WaitingAside; _nextBark = Time.time + UnityEngine.Random.Range(40f, 90f); });
        }

        /// <summary>Player let go: wait 1 s, walk home, carry on.</summary>
        public void WalkBack(Transform workPoint)
        {
            _waitBefore = Time.time + 1f;
            WalkTo(workPoint, State.WalkingBack, () => { Current = State.Working; _nextBark = Time.time + 2f; });
        }

        /// <summary>Bot's own trips (no money): walk to the door, then ride.</summary>
        public void WalkToDoor(Transform door, Action arrived) => WalkTo(door, State.WalkingToDoor, arrived);

        /// <summary>Hang on the bus door. Parenting to the bus is riding, not teleporting: the bot is already at the door.</summary>
        public void Ride(Transform doorSeat)
        {
            StopAgent();
            Current = State.Riding;
            transform.SetParent(doorSeat, true);
            _target = null;
        }

        public void Alight(Vector3 at)
        {
            transform.SetParent(null, true);
            Current = State.Working;
        }

        public void FaceWhileIdle(Transform t) { faceWhenIdle = t; }

        void WalkTo(Transform target, State s, Action arrived)
        {
            if (!target) { arrived?.Invoke(); return; }
            Current = s;
            _target = target;
            _onArrive = arrived;
            _retryAt = 0f;
            if (UseAgent())
            {
                agent.speed = WalkSpeed;
                agent.isStopped = Time.time < _waitBefore;
                if (!agent.SetDestination(target.position)) _retryAt = Time.time + RetrySeconds;
            }
        }

        void Update()
        {
            float speed = 0f;
            if (_target && Current != State.Riding)
            {
                if (Time.time < _waitBefore) { /* short pause before walking back */ }
                else if (UseAgent()) speed = TickAgent();
                else speed = TickStraight();
            }
            else if (faceWhenIdle && (Current == State.WaitingAside || Current == State.Working))
            {
                Vector3 d = faceWhenIdle.position - transform.position; d.y = 0f;
                if (d.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), 180f * Time.deltaTime);
            }
            if (animator) animator.SetFloat("Speed", speed);

            if (Time.time >= _nextBark && (Current == State.Working || Current == State.WaitingAside || Current == State.Riding))
            {
                bool aside = Current == State.WaitingAside;
                Bark(aside ? AsideBarks[UnityEngine.Random.Range(0, AsideBarks.Length)] : WorkBarks[UnityEngine.Random.Range(0, WorkBarks.Length)]);
                _nextBark = Time.time + (aside ? UnityEngine.Random.Range(40f, 90f) : UnityEngine.Random.Range(6f, 10f));
            }
        }

        float TickStraight()
        {
            Vector3 to = _target.position - transform.position; to.y = 0f;
            float d = to.magnitude;
            if (d <= ArriveDist) { Arrive(); return 0f; }
            Vector3 step = to / d * Mathf.Min(d, WalkSpeed * Time.deltaTime);
            transform.position += step;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), 360f * Time.deltaTime);
            return WalkSpeed;
        }

        float TickAgent()
        {
            agent.isStopped = false;
            if (_retryAt > 0f)
            {
                // Path failed: stay put and try again every 2 s. Never snap.
                if (Time.time < _retryAt) return 0f;
                _retryAt = agent.SetDestination(_target.position) ? 0f : Time.time + RetrySeconds;
                return 0f;
            }
            if (!agent.pathPending && agent.remainingDistance <= Mathf.Max(ArriveDist, agent.stoppingDistance)) { Arrive(); return 0f; }
            return agent.velocity.magnitude;
        }

        void Arrive()
        {
            StopAgent();
            _target = null;
            Action a = _onArrive; _onArrive = null;
            a?.Invoke();
        }

        bool UseAgent() => agent && agent.enabled && agent.isOnNavMesh;
        void StopAgent() { if (UseAgent()) agent.isStopped = true; }

        void Bark(string line) { Barked?.Invoke(this, line); }
    }
}
