using System.Collections.Generic;
using UnityEngine;

namespace AmazonExpedition.AI
{
    public enum TeammateState { Idle, Explore, Admire, Greet, Follow, React }

    [RequireComponent(typeof(CharacterController))]
    [DisallowMultipleComponent]
    public sealed class TeammateAI : MonoBehaviour
    {
        [Header("Identity")]
        public string displayName = "MARCO";

        [Header("References")]
        [SerializeField] private Transform eyes;
        [SerializeField] private Transform playerRoot;
        [SerializeField] private Animator animator;
        [SerializeField] private LayerMask groundMask = ~0;

        [Header("Home")]
        [Tooltip("Centre of the area this teammate explores.")]
        [SerializeField] private Vector3 homeOffset = Vector3.zero;
        [SerializeField] private float homeRadius = 26f;
        [SerializeField] private bool followDistantPlayer = true;
        [SerializeField] private float followDistance = 100f;
        [SerializeField] private float keepAway = 6f;

        [Header("Movement")]
        [SerializeField] private float walkSpeed = 1.7f;
        [SerializeField] private float runSpeed = 4.4f;
        [SerializeField] private float acceleration = 6f;
        [SerializeField] private float gravity = 22f;
        [SerializeField] private float stepHeight = 0.3f;

        [Header("Vision & Avoidance")]
        [SerializeField] private float viewDistance = 12f;
        [SerializeField] private float viewAngle = 70f;
        [SerializeField] private LayerMask obstacleMask = ~0;
        [SerializeField] private float avoidForce = 3f;

        [Header("Animation")]
        [SerializeField] private string speedParam = "Speed";
        [SerializeField] private string groundedParam = "Grounded";
        [SerializeField] private string waveParam = "Wave";
        [SerializeField] private string admireParam = "Admire";

        [Header("Vfx")]
        [SerializeField] private ParticleSystem footfallDust;
        [SerializeField] private float footDustEvery = 0.35f;

        public TeammateState State { get; private set; }
        public string Name { get { return displayName; } }
        public bool IsGreeting { get { return State == TeammateState.Greet; } }
        public bool IsAdmiring { get { return State == TeammateState.Admire; } }
        public float WalkSpeed { get { return walkSpeed; } }
        public float RunSpeed { get { return runSpeed; } }

        private CharacterController controller;
        private readonly List<Vector3> wanderQueue = new List<Vector3>(8);
        private Vector3 target;
        private Vector3 velocity;
        private float vertical;
        private float stateTimer;
        private float greetCooldown;
        private Vector3 smoothedWish = Vector3.zero;
        private float dustTimer;
        private float stridePhase;
        private Vector3 previousPos;
        private float currentSpeed;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (eyes == null) eyes = transform;
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (playerRoot == null)
            {
                var p = GameObject.FindWithTag("Player");
                if (p != null) playerRoot = p.transform;
            }
            Enter(TeammateState.Explore);
            PickTarget(transform.position);
            previousPos = transform.position;
        }

        public void Teleport(Vector3 position, bool snapToGround)
        {
            controller.enabled = false;
            transform.position = position;
            if (snapToGround) transform.position = new Vector3(position.x, SampleGround(position), position.z);
            controller.enabled = true;
            PickTarget(transform.position);
        }

        private float SampleGround(Vector3 position)
        {
            RaycastHit hit;
            if (Physics.Raycast(position + Vector3.up * 2f, Vector3.down, out hit, 6f, groundMask, QueryTriggerInteraction.Ignore))
                return hit.point.y;
            return position.y;
        }

        public void Enter(TeammateState next)
        {
            State = next;
            stateTimer = 0f;
            greetCooldown = next == TeammateState.Greet ? 26f : greetCooldown;
            if (next == TeammateState.Explore) PickTarget(transform.position);
        }

        private void PickTarget(Vector3 from)
        {
            for (var i = 0; i < 12; i++)
            {
                var candidate = new Vector3(
                    homeOffset.x + Random.Range(-homeRadius, homeRadius),
                    0f,
                    homeOffset.z + Random.Range(-homeRadius, homeRadius));
                candidate.y = from.y;
                if (Vector3.Distance(candidate, from) < 1.5f) continue;
                target = candidate;
                return;
            }
            target = from;
        }

        private void Update()
        {
            stateTimer += Time.deltaTime;
            greetCooldown = Mathf.Max(0f, greetCooldown - Time.deltaTime);
            UpdateState();
            Move(Time.deltaTime);
            Animate(Time.deltaTime);
        }

        private void UpdateState()
        {
            var toPlayer = playerRoot != null ? playerRoot.position - transform.position : Vector3.zero;
            var playerDistance = toPlayer.magnitude;

            switch (State)
            {
                case TeammateState.Explore:
                    if (Vector3.Distance(target, transform.position) < 1.4f) Enter(TeammateState.Admire);
                    break;

                case TeammateState.Admire:
                    LookForBeauty();
                    if (stateTimer > 3.5f) Enter(TeammateState.Explore);
                    break;

                case TeammateState.Greet:
                    if (stateTimer > 2.6f) Enter(TeammateState.Explore);
                    break;

                case TeammateState.Follow:
                    if (playerDistance < keepAway) Enter(TeammateState.Explore);
                    break;
            }

            if (playerDistance < 8f && greetCooldown == 0f && State != TeammateState.Greet
                && CanSee(playerRoot != null ? playerRoot.position : transform.position))
            {
                Enter(TeammateState.Greet);
            }

            if (followDistantPlayer && playerDistance > followDistance && State != TeammateState.Follow
                && State != TeammateState.Greet && State != TeammateState.Admire)
            {
                Enter(TeammateState.Follow);
            }
        }

        private bool CanSee(Vector3 point)
        {
            if (eyes == null) return false;
            var direction = (point - eyes.position);
            var distance = direction.magnitude;
            if (distance > viewDistance) return false;
            direction.Normalize();
            if (Vector3.Angle(eyes.forward, direction) > viewAngle) return false;
            return !Physics.Raycast(eyes.position, direction, distance, obstacleMask, QueryTriggerInteraction.Ignore);
        }

        private void LookForBeauty()
        {
            if (eyes == null) return;
            RaycastHit hit;
            if (Physics.Raycast(eyes.position, eyes.forward, out hit, viewDistance, groundMask, QueryTriggerInteraction.Ignore))
            {
                var look = Quaternion.LookRotation(-hit.normal, Vector3.up);
                eyes.rotation = Quaternion.Slerp(eyes.rotation, look, Time.deltaTime);
            }
        }

        private void Move(float dt)
        {
            var wishSpeed = State == TeammateState.Follow ? runSpeed : walkSpeed;
            var wish = Vector3.zero;

            if (State == TeammateState.Explore || State == TeammateState.Follow)
            {
                var destination = State == TeammateState.Follow && playerRoot != null
                    ? playerRoot.position
                    : target;
                var to = destination - transform.position;
                to.y = 0f;
                if (to.sqrMagnitude > 0.0001f)
                {
                    to.Normalize();
                    wish = to * wishSpeed;
                }
            }

            wish += AvoidObstacles() * avoidForce;
            smoothedWish = Vector3.MoveTowards(smoothedWish, wish, acceleration * dt);

            var horizontal = smoothedWish;
            if (horizontal.magnitude > wishSpeed) horizontal = horizontal.normalized * wishSpeed;
            velocity = horizontal;

            vertical -= gravity * dt;
            if (controller.isGrounded) vertical = -0.05f;

            controller.Move((velocity + Vector3.up * vertical) * dt);

            if (velocity.sqrMagnitude > 0.01f)
            {
                var face = Quaternion.LookRotation(velocity.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, face, dt * 6f);
            }

            if (controller.isGrounded && velocity.sqrMagnitude > 0.4f)
            {
                dustTimer += dt * velocity.magnitude;
                if (dustTimer > footDustEvery)
                {
                    dustTimer = 0f;
                    stridePhase += 1f;
                    if (footfallDust != null)
                    {
                        footfallDust.transform.position = transform.position;
                        footfallDust.Emit(2);
                    }
                }
            }
        }

        private Vector3 AvoidObstacles()
        {
            var steering = Vector3.zero;
            var forward = velocity.sqrMagnitude > 0.001f ? velocity.normalized : transform.forward;

            for (var i = -1; i <= 1; i++)
            {
                var angle = i * 35f;
                var direction = Quaternion.Euler(0f, angle, 0f) * forward;
                if (!Physics.Raycast(transform.position + Vector3.up * 0.5f, direction, 1.4f, obstacleMask, QueryTriggerInteraction.Ignore))
                    continue;
                steering -= direction * 0.5f;
                steering += Quaternion.Euler(0f, Mathf.Sign(i) * -70f, 0f) * forward;
            }

            return steering;
        }

        private void Animate(float dt)
        {
            currentSpeed = velocity.magnitude;
            stridePhase += currentSpeed * dt * 1.6f;

            if (animator == null || !animator.enabled) return;
            animator.SetFloat(speedParam, Mathf.Clamp01(currentSpeed / Mathf.Max(0.001f, runSpeed)), 0.12f, dt);
            animator.SetBool(groundedParam, controller.isGrounded);
            animator.SetBool(waveParam, State == TeammateState.Greet);
            animator.SetBool(admireParam, State == TeammateState.Admire);
        }


        private void OnDrawGizmosSelected()
        {
            Gizmos.color = State == TeammateState.Greet ? Color.yellow : Color.cyan;
            var centre = Application.isPlaying ? transform.position : homeOffset;
            Gizmos.DrawWireSphere(centre, homeRadius);
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(Application.isPlaying ? target : homeOffset, 1.4f);
        }
    }
}
