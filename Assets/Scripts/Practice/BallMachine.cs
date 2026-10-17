using System;
using BeachVolley.Ball;
using BeachVolley.Court;
using BeachVolley.Player;
using UnityEngine;
using Random = UnityEngine.Random;

namespace BeachVolley.Practice
{
    public enum MachineMode { Off, FreeBalls, Serves, SetsToSpike, Rally }
    public enum MachinePace { Slow, Normal, Fast }

    /// <summary>
    /// Practice partner: a ball launcher that feeds the player. It moves to where each shot comes
    /// from and fires with the same flight math as the player's own shots.
    /// - Free balls: easy high balls from the far court to a random spot on your half.
    /// - Serves: from the far baseline, alternating driven and floated serves.
    /// - Sets to spike: from your side by the net, a high set ~1 m off the net to attack.
    /// - Rally: whatever you send over comes back as a free ball from where it lands, endlessly.
    /// The next ball is fired a pace-dependent moment after the previous one dies on the sand.
    /// </summary>
    public class BallMachine : MonoBehaviour
    {
        [SerializeField] VolleyBall ball;
        [SerializeField] Transform player;
        [SerializeField] BallContact contact;
        [SerializeField] PlayerServe serve;
        [Tooltip("The launcher model; moved to wherever the next shot comes from.")]
        [SerializeField] Transform visual;

        [SerializeField] MachineMode mode = MachineMode.FreeBalls;
        [SerializeField] MachinePace pace = MachinePace.Normal;
        [Tooltip("Seconds after the ball dies before the next shot: slow, normal, fast.")]
        [SerializeField] Vector3 paceDelays = new Vector3(4.5f, 2.8f, 1.5f);
        [Tooltip("Fire anyway if a ball has been live this long (stuck somewhere).")]
        [SerializeField] float stuckTimeout = 12f;
        [SerializeField] float muzzleHeight = 1.2f;

        [Header("Shots")]
        [SerializeField] float freeBallApex = 4.6f;
        [SerializeField] float serveContactHeight = 2.7f;
        [SerializeField] float servePowerSpeed = 12f;
        [SerializeField] float serveMinSpeed = 8f;
        [SerializeField] float serveFloatApex = 4.2f;
        [Tooltip("Sets peak this high (spike reach in a jump is ~3.2 m).")]
        [SerializeField] float setApex = 4.9f;
        [Tooltip("Sets come down this far from the net, this far along it from the machine.")]
        [SerializeField] float setOffNet = 1f;
        [SerializeField] float setAlongNet = 2.5f;
        [SerializeField] float machineSetX = 3f;
        [SerializeField] float rallyApex = 4.4f;
        [Tooltip("In rally mode, a ball on the far side is returned once it drops below this height.")]
        [SerializeField] float rallyReturnHeight = 1.3f;

        float nextShotTime;
        float lastShotTime = float.NegativeInfinity;
        bool awaitingReturn;
        bool nextServeFloat;
        bool paused;

        /// <summary>Raised whenever the machine fires a ball.</summary>
        public event Action Fired;

        public MachineMode Mode
        {
            get => mode;
            set
            {
                mode = value;
                awaitingReturn = false;
                nextShotTime = Time.time + 1f;
                MoveVisualToStart();
            }
        }

        public MachinePace Pace
        {
            get => pace;
            set => pace = value;
        }

        public bool Paused
        {
            get => paused;
            set
            {
                paused = value;
                if (!paused) nextShotTime = Time.time + 1f;
            }
        }

        float Delay => pace switch { MachinePace.Slow => paceDelays.x, MachinePace.Fast => paceDelays.z, _ => paceDelays.y };
        /// <summary>−1 if the player is on the −Z half, +1 otherwise.</summary>
        float PlayerSide => player.position.z <= 0f ? -1f : 1f;
        float FarSide => -PlayerSide;
        static float NetTop => CourtDimensions.NetHeight + VolleyBall.Radius + 0.25f;

        void Awake()
        {
            if (ball == null) ball = FindAnyObjectByType<VolleyBall>();
            if (contact == null) contact = FindAnyObjectByType<BallContact>();
            if (serve == null) serve = FindAnyObjectByType<PlayerServe>();
            if (player == null && contact != null) player = contact.transform;
        }

        void OnEnable()
        {
            if (contact != null) contact.Played += OnPlayerPlayed;
            if (serve != null) serve.Hit += OnPlayerServed;
            if (ball != null) ball.Landed += OnLanded;
            nextShotTime = Time.time + 1.5f;
            MoveVisualToStart();
        }

        void OnDisable()
        {
            if (contact != null) contact.Played -= OnPlayerPlayed;
            if (serve != null) serve.Hit -= OnPlayerServed;
            if (ball != null) ball.Landed -= OnLanded;
        }

        void OnPlayerPlayed(HitType type, float height) => awaitingReturn = true;
        void OnPlayerServed(float side) => awaitingReturn = true;

        void OnLanded(Vector3 point)
        {
            nextShotTime = Time.time + Delay;
            // In a rally a ball that reached the far side gets returned from where it landed.
            if (mode == MachineMode.Rally && awaitingReturn && Mathf.Sign(point.z) == FarSide && !paused)
                ReturnFrom(point);
        }

        void Update()
        {
            if (ball == null || player == null || mode == MachineMode.Off || paused) return;
            // Don't fire over the top of the player's own serve toss.
            if (serve != null && serve.IsTossLive) return;

            if (mode == MachineMode.Rally && awaitingReturn && !ball.IsDead)
            {
                Vector3 p = ball.transform.position;
                if (Mathf.Sign(p.z) == FarSide && Mathf.Abs(p.z) > 0.5f && p.y < rallyReturnHeight && ball.Body.linearVelocity.y < 0f)
                {
                    ReturnFrom(p);
                    return;
                }
            }

            bool stuck = !ball.IsDead && Time.time - lastShotTime > stuckTimeout;
            if ((ball.IsDead || stuck) && Time.time >= nextShotTime) Fire();
        }

        /// <summary>Fires the next ball for the current mode now.</summary>
        public void Fire()
        {
            switch (mode)
            {
                case MachineMode.FreeBalls:
                case MachineMode.Rally:
                    FreeBall(new Vector3(Random.Range(-2f, 2f), muzzleHeight, FarSide * 5.5f));
                    break;
                case MachineMode.Serves:
                    Serve();
                    break;
                case MachineMode.SetsToSpike:
                    Set();
                    break;
            }
        }

        void FreeBall(Vector3 origin)
        {
            var target = new Vector3(Random.Range(-3f, 3f), VolleyBall.Radius, PlayerSide * Random.Range(2f, 6f));
            Shoot(origin, ball.ArcOverNet(origin, target, freeBallApex, NetTop));
        }

        void Serve()
        {
            var origin = new Vector3(Random.Range(-2f, 2f), serveContactHeight, FarSide * (CourtDimensions.SideLength + 0.6f));
            var target = new Vector3(Random.Range(-3f, 3f), VolleyBall.Radius, PlayerSide * Random.Range(4f, 7f));
            Vector3 v = nextServeFloat
                ? ball.ArcOverNet(origin, target, serveFloatApex, NetTop)
                : ball.DriveOverNet(origin, target, NetTop, servePowerSpeed, serveMinSpeed);
            nextServeFloat = !nextServeFloat;
            Shoot(origin, v);
        }

        /// <summary>A high ball that peaks over a spot ~1 m off the net, on the player's side.</summary>
        void Set()
        {
            float x = machineSetX;
            var origin = new Vector3(x, muzzleHeight, PlayerSide * 0.8f);
            var spot = new Vector3(x - Mathf.Sign(x) * setAlongNet, setApex, PlayerSide * setOffNet);
            float g = ball.GravityMagnitude;
            float vy = Mathf.Sqrt(2f * g * (setApex - origin.y));
            float rise = vy / g;
            var horizontal = new Vector3(spot.x - origin.x, 0f, spot.z - origin.z) / rise;
            Shoot(origin, horizontal + Vector3.up * vy);
        }

        /// <summary>Rally return: a free ball back to the player's half from where theirs came down.</summary>
        void ReturnFrom(Vector3 point)
        {
            float half = CourtDimensions.HalfWidth - 0.3f;
            var origin = new Vector3(Mathf.Clamp(point.x, -half, half), muzzleHeight,
                FarSide * Mathf.Clamp(Mathf.Abs(point.z), 1.5f, CourtDimensions.SideLength - 0.3f));
            var target = new Vector3(Random.Range(-3f, 3f), VolleyBall.Radius, PlayerSide * Random.Range(2f, 6f));
            Shoot(origin, ball.ArcOverNet(origin, target, rallyApex, NetTop));
        }

        void Shoot(Vector3 origin, Vector3 velocity)
        {
            MoveVisual(origin, velocity);
            ball.Place(origin);
            ball.Launch(velocity);
            lastShotTime = Time.time;
            awaitingReturn = false;
            Fired?.Invoke();
        }

        void MoveVisualToStart()
        {
            if (player == null) return;
            Vector3 at = mode switch
            {
                MachineMode.SetsToSpike => new Vector3(machineSetX, 0f, PlayerSide * 0.8f),
                MachineMode.Serves => new Vector3(0f, 0f, FarSide * (CourtDimensions.SideLength + 0.6f)),
                _ => new Vector3(0f, 0f, FarSide * 5.5f),
            };
            MoveVisual(at, new Vector3(0f, 0f, PlayerSide));
            if (visual != null) visual.gameObject.SetActive(mode != MachineMode.Off);
        }

        void MoveVisual(Vector3 origin, Vector3 aim)
        {
            if (visual == null) return;
            visual.position = new Vector3(origin.x, 0f, origin.z);
            aim.y = 0f;
            if (aim.sqrMagnitude > 1e-4f) visual.rotation = Quaternion.LookRotation(aim);
        }
    }
}
