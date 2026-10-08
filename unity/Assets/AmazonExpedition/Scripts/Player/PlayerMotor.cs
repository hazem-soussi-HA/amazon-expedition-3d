using System.Collections.Generic;
using AmazonExpedition.Environment;
using UnityEngine;

namespace AmazonExpedition.Player
{
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    public sealed class PlayerMotor : MonoBehaviour
    {
        public struct GroundInfo
        {
            public bool grounded;
            public bool stable;
            public bool tooSteep;
            public float distance;
            public float angle;
            public float slope;
            public Vector3 normal;
            public Vector3 point;
            public Collider collider;
            public SurfaceProfile surface;
            public float sink;
        }

        [Header("Body")]
        [SerializeField] private float height = 1.78f;
        [SerializeField] private float radius = 0.3f;
        [SerializeField] private float skinWidth = 0.015f;
        [SerializeField] private float stepOffset = 0.42f;
        [SerializeField, Range(1f, 89f)] private float slopeLimit = 48f;
        [SerializeField] private float groundSnap = 0.35f;
        [SerializeField] private LayerMask collisionMask = ~0;

        [Header("Locomotion")]
        [SerializeField] private float walkSpeed = 2.4f;
        [SerializeField] private float jogSpeed = 5.4f;
        [SerializeField] private float sprintSpeed = 8.6f;
        [SerializeField] private float groundAcceleration = 16f;
        [SerializeField] private float groundDeceleration = 22f;
        [SerializeField, Range(0f, 1f)] private float airControl = 0.3f;
        [SerializeField] private float turnResponse = 11f;
        [SerializeField] private float maxTurnRate = 900f;
        [SerializeField, Range(0f, 1f)] private float turnInPlaceGate = 0.25f;

        [Header("Gravity & Grounding")]
        [SerializeField] private float gravity = 22f;
        [SerializeField] private float terminalVelocity = 55f;
        [SerializeField] private float groundedCheckDistance = 0.19f;
        [SerializeField] private float coyoteTime = 0.12f;
        [SerializeField] private float jumpBuffer = 0.15f;
        [SerializeField] private float airTimeToFall = 0.25f;

        [Header("Water / Buoyancy")]
        [SerializeField] private float waterDrag = 2.4f;
        [SerializeField] private float waterAcceleration = 5f;
        [SerializeField] private float buoyancyPerMetre = 34f;
        [SerializeField] private float maxBuoyancy = 60f;

        [Header("Impulses")]
        [SerializeField] private float impulseDecay = 6f;
        [SerializeField] private float maxStumbleImpulse = 14f;

        private Rigidbody body;
        private CapsuleCollider capsule;
        private readonly List<Vector3> impulseForces = new List<Vector3>(8);
        private readonly List<float> impulseLife = new List<float>(8);
        private float lastGroundedTime = -100f;
        private float lastJumpPressed = -100f;
        private bool jumpRequested;
        private Vector3 planarVelocity;
        private float verticalVelocity;
        private float stumbleShake;
        private bool wasGrounded;

        public GroundInfo Ground { get; private set; }
        public Vector3 PlanarVelocity { get { return planarVelocity; } }
        public float VerticalVelocity { get { return verticalVelocity; } }
        public float Speed { get { return planarVelocity.magnitude; } }
        public float SprintSpeed { get { return sprintSpeed; } }
        public float JogSpeed { get { return jogSpeed; } }
        public float WalkSpeed { get { return walkSpeed; } }
        public float WaterDepth { get; private set; }
        public bool InWater { get { return WaterDepth > 0.05f; } }
        public bool Grounded { get { return Ground.grounded; } }
        public bool Moving { get { return inputMove.sqrMagnitude > 0.0001f; } }
        public float StumbleShake { get { return stumbleShake; } }
        public float BodyHeight { get { return height; } }
        public float BodyRadius { get { return radius; } }
        public CollisionFlags CollisionState { get { return collisionState; } }
        public bool Sprinting { get { return sprintEngaged; } }
        public Vector3 Position { get { return transform.position; } }

        private CollisionFlags collisionState;
        private Vector3 inputMove;
        private bool inputJump;
        private bool inputSprint;
        private bool sprintEngaged;
        private float stamina01 = 1f;
        private float maxStamina = 100f;
        private float stamina = 100f;
        public float Stamina { get { return stamina; } }
        public float Stamina01 { get { return stamina01; } }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            capsule = GetComponent<CapsuleCollider>();
            ConfigureRigidbody();
            Ground = new GroundInfo();
        }

        private void ConfigureRigidbody()
        {
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.constraints = RigidbodyConstraints.FreezeRotation;

            if (capsule != null)
            {
                capsule.height = height;
                capsule.radius = radius;
                capsule.center = new Vector3(0f, height * 0.5f, 0f);
                capsule.direction = 1;
                capsule.isTrigger = false;
            }
        }

        public void SetInput(Vector2 move, bool jump, bool sprint)
        {
            inputMove = new Vector3(move.x, 0f, move.y);
            if (inputMove.sqrMagnitude > 1f) inputMove.Normalize();
            inputJump = jump;
            inputSprint = sprint;
        }

        public void RequestJump()
        {
            jumpRequested = true;
            lastJumpPressed = Time.time;
        }

        public void AddImpulse(Vector3 impulse, float life = 0.6f)
        {
            var dir = impulse;
            if (dir.magnitude > maxStumbleImpulse) dir = dir.normalized * maxStumbleImpulse;
            impulseForces.Add(dir);
            impulseLife.Add(life);
            stumbleShake = Mathf.Min(1f, stumbleShake + dir.magnitude / maxStumbleImpulse);
        }

        public void SetWaterDepth(float depth)
        {
            WaterDepth = Mathf.Max(0f, depth);
        }

        public void DrainStamina(float amount)
        {
            stamina = Mathf.Clamp(stamina - amount, 0f, maxStamina);
            stamina01 = maxStamina > 0f ? stamina / maxStamina : 0f;
        }

        public void RestoreStamina(float amount)
        {
            stamina = Mathf.Clamp(stamina + amount, 0f, maxStamina);
            stamina01 = maxStamina > 0f ? stamina / maxStamina : 0f;
        }

        private void FixedUpdate()
        {
            if (jumpRequested) { lastJumpPressed = Time.time; jumpRequested = false; }
            ProbeGround();
            IntegrateExternalImpulses(Time.fixedDeltaTime);
            Move(Time.fixedDeltaTime);
            UpdateWaterState();
            UpdateStamina(Time.fixedDeltaTime);
            wasGrounded = Ground.grounded;
        }

        private void IntegrateExternalImpulses(float dt)
        {
            var accumulated = Vector3.zero;
            for (var i = impulseForces.Count - 1; i >= 0; i--)
            {
                impulseLife[i] -= dt;
                if (impulseLife[i] <= 0f)
                {
                    impulseForces.RemoveAt(i);
                    impulseLife.RemoveAt(i);
                    continue;
                }
                accumulated += impulseForces[i];
            }

            var planar = accumulated;
            planar.y = 0f;
            planarVelocity += planar * dt;

            if (!Ground.grounded && accumulated.y > 0f)
                verticalVelocity += accumulated.y * dt;
        }

        private void ProbeGround()
        {
            var origin = transform.position;
            var bottom = origin + new Vector3(0f, radius + skinWidth, 0f);
            var down = -transform.up;
            var mask = collisionMask;

            RaycastHit hit;
            var grounded = false;
            var normal = Vector3.up;
            var point = origin;
            var distance = 0f;
            Collider hitCollider = null;

            if (Physics.SphereCast(bottom, radius - 0.01f, down, out hit, groundedCheckDistance + skinWidth, mask, QueryTriggerInteraction.Ignore))
            {
                var slope = Vector3.Angle(hit.normal, Vector3.up);
                if (slope <= slopeLimit + 2f)
                {
                    grounded = true;
                    normal = hit.normal;
                    point = hit.point;
                    distance = hit.distance;
                    hitCollider = hit.collider;
                }
            }

            var info = new GroundInfo();
            info.collider = hitCollider;
            info.normal = normal;
            info.point = point;
            info.distance = distance;
            info.angle = Vector3.Angle(normal, Vector3.up);
            info.slope = info.angle;
            info.tooSteep = grounded && info.angle > slopeLimit;
            info.grounded = grounded && !info.tooSteep;
            info.stable = info.grounded;
            info.surface = SurfaceResolver.Resolve(point, defaultSurface, Ground.surface);
            info.sink = info.surface != null ? info.surface.sinkDepth : 0f;

            Ground = info;
            if (info.grounded) lastGroundedTime = Time.time;
            if (info.grounded && verticalVelocity < 0f) verticalVelocity = 0f;
        }

        [Tooltip("Used when the motor cannot resolve a surface profile from the world.")]
        [SerializeField] private SurfaceProfile defaultSurface;

        private void Move(float dt)
        {
            var worldInput = inputMove;
            var wishSpeed = WishSpeed();
            var traction = Ground.surface != null ? Ground.surface.traction : 1f;
            var drag = Ground.surface != null ? Ground.surface.drag : 0f;

            var wish = worldInput * wishSpeed;
            var control = Ground.grounded ? 1f : airControl;
            var accel = groundAcceleration * control * traction;
            var decel = groundDeceleration * control * traction;

            if (worldInput.sqrMagnitude > 0.0001f)
                planarVelocity = Vector3.MoveTowards(planarVelocity, wish, accel * dt);
            else
                planarVelocity = Vector3.MoveTowards(planarVelocity, Vector3.zero, decel * dt);

            if (drag > 0f)
                planarVelocity = Vector3.MoveTowards(planarVelocity, Vector3.zero, drag * dt * (Ground.grounded ? 1f : 0.25f));

            if (Ground.grounded)
                planarVelocity = ProjectOnSlope(planarVelocity);

            if (InWater && verticalVelocity < 0f)
            {
                var sub = Mathf.Clamp01(WaterDepth - radius);
                verticalVelocity += buoyancyPerMetre * sub * dt;
                verticalVelocity *= 1f / (1f + waterDrag * dt);
                if (verticalVelocity > maxBuoyancy) verticalVelocity = maxBuoyancy;
            }

            if (!Ground.grounded)
            {
                var sinceGrounded = Time.time - lastGroundedTime;
                var sincePressed = Time.time - lastJumpPressed;
                if (sinceGrounded < coyoteTime && sincePressed < jumpBuffer && strapJump == false)
                    PerformJump();

                verticalVelocity -= gravity * dt;
                if (verticalVelocity < -terminalVelocity) verticalVelocity = -terminalVelocity;
            }

            var delta = planarVelocity + Vector3.up * verticalVelocity;
            var travel = delta * dt;

            var flags = CollisionFlags.None;
            var position = transform.position;

            if (travel != Vector3.zero)
            {
                position = ApplyStepAndMove(position, travel, ref flags);
            }
            else
            {
                flags |= CheckCollisionOnly(position);
            }

            if (Ground.grounded || wasGrounded)
            {
                var snap = GroundDown(position);
                if (snap.y < position.y) position = snap;
            }

            body.MovePosition(position);
            transform.position = position;
            collisionState = flags;

            if (transform.position.y < -60f) Respawn();
        }

        private bool strapJump;

        private Vector3 ProjectOnSlope(Vector3 velocity)
        {
            if (Ground.normal == Vector3.up || !Ground.grounded) return velocity;
            var projected = Vector3.ProjectOnPlane(velocity, Ground.normal);
            var downhill = -new Vector3(Ground.normal.x, 0f, Ground.normal.z).normalized;
            projected += downhill * (Mathf.Sin(Ground.angle * Mathf.Deg2Rad) * gravity * 0.42f) * Time.fixedDeltaTime;
            return projected;
        }

        private void PerformJump()
        {
            verticalVelocity = 8.4f;
            lastGroundedTime = -100f;
            lastJumpPressed = -100f;
            Ground = new GroundInfo();
        }

        private float WishSpeed()
        {
            var wantsSprint = inputSprint && stamina01 > 0.05f;
            sprintEngaged = wantsSprint && inputMove.sqrMagnitude > 0.01f && Ground.grounded;
            var baseSpeed = sprintEngaged ? sprintSpeed : (inputMove.sqrMagnitude > 0.5f ? jogSpeed : walkSpeed);
            if (InWater) baseSpeed = Mathf.Min(baseSpeed, jogSpeed * 0.55f);
            return baseSpeed;
        }

        private Vector3 ApplyStepAndMove(Vector3 from, Vector3 travel, ref CollisionFlags flags)
        {
            var traveled = travel;
            if (!SweepMove(from, ref traveled, out var blocked))
            {
                if (blocked && Ground.grounded && travel.sqrMagnitude > 0.0001f)
                {
                    if (TryStepUp(from, travel, out var stepped))
                    {
                        flags |= CollisionFlags.Sides;
                        return stepped;
                    }
                }
                flags |= CollisionFlags.Sides;
                if (travel.y <= 0f) flags |= CollisionFlags.Below;
                return from + traveled;
            }

            if (travel.y < 0f) flags |= CollisionFlags.Below;
            return from + traveled;
        }

        private bool TryStepUp(Vector3 from, Vector3 travel, out Vector3 result)
        {
            result = from;
            var probeRadius = radius * 0.92f;
            var p1 = from + Vector3.up * (probeRadius + 0.02f);
            var p2 = from + Vector3.up * (height - probeRadius - 0.02f);

            RaycastHit hit;
            if (!Physics.CapsuleCast(p1, p2, probeRadius, travel.normalized, out hit, travel.magnitude + probeRadius, collisionMask, QueryTriggerInteraction.Ignore))
                return false;

            var stepHeight = hit.point.y - from.y;
            if (stepHeight <= 0f || stepHeight > stepOffset) return false;

            var upTarget = from + Vector3.up * (stepHeight + 0.02f);
            var move = travel;
            if (!SweepMove(upTarget, ref move, out var blockedUp)) return false;

            var down = upTarget + move - Vector3.up * (stepHeight + 0.1f);
            if (Physics.CapsuleCast(down + Vector3.up * (height - probeRadius), down + Vector3.up * probeRadius, probeRadius, Vector3.down, out RaycastHit dummy, 0.5f, collisionMask, QueryTriggerInteraction.Ignore))
            {
                result = upTarget + move;
                planarVelocity = planarVelocity * 0.98f;
                return true;
            }

            return false;
        }

        private CollisionFlags CheckCollisionOnly(Vector3 position)
        {
            var flags = CollisionFlags.None;
            var probeRadius = radius - skinWidth;
            var p1 = position + Vector3.up * (probeRadius + 0.02f);
            var p2 = position + Vector3.up * (height - probeRadius - 0.02f);

            RaycastHit hit;
            if (Physics.CapsuleCast(p1, p2, probeRadius, Vector3.down, out hit, groundedCheckDistance, collisionMask, QueryTriggerInteraction.Ignore))
                flags |= CollisionFlags.Below;
            if (Physics.CheckSphere(position + Vector3.up * radius, probeRadius, collisionMask, QueryTriggerInteraction.Ignore))
                flags |= CollisionFlags.Sides;

            return flags;
        }

        private bool SweepMove(Vector3 from, ref Vector3 travel, out bool blocked)
        {
            blocked = false;
            var probe = radius - skinWidth;
            var p1 = from + Vector3.up * (probe + 0.01f);
            var p2 = from + Vector3.up * (height - probe - 0.01f);
            var distance = travel.magnitude;
            if (distance < 0.0001f) return true;

            var dir = travel / distance;

            if (Physics.CapsuleCast(p1, p2, probe, dir, out RaycastHit hit, distance + skinWidth, collisionMask, QueryTriggerInteraction.Ignore))
            {
                var remaining = Mathf.Max(0f, hit.distance - skinWidth);
                travel = dir * remaining;
                blocked = true;
                return false;
            }

            return true;
        }

        private Vector3 GroundDown(Vector3 position)
        {
            var probe = radius;
            var origin = position + Vector3.up * (groundSnap + probe);
            RaycastHit hit;
            if (Physics.SphereCast(origin, probe - 0.01f, Vector3.down, out hit, groundSnap + skinWidth, collisionMask, QueryTriggerInteraction.Ignore))
            {
                var snap = hit.point.y;
                if (snap < position.y) return new Vector3(position.x, snap, position.z);
            }
            return position;
        }

        private void UpdateWaterState()
        {
            var depth = WaterVolume.SampleDepth(transform.position + Vector3.up * 0.1f, height);
            SetWaterDepth(depth);
            if (depth > 0f) DrainStamina(6f * depth * Time.fixedDeltaTime);
        }

        private void UpdateStamina(float dt)
        {
            if (sprintEngaged && Moving)
            {
                DrainStamina(11f * dt);
                if (stamina01 <= 0.001f) sprintEngaged = false;
            }
            else
            {
                RestoreStamina(Ground.grounded ? 16f * dt : 8f * dt);
            }

            stumbleShake = Mathf.Max(0f, stumbleShake - dt * 2.2f);
        }

        private void Respawn()
        {
            transform.position = new Vector3(0f, 1f, 180f);
            planarVelocity = Vector3.zero;
            verticalVelocity = 0f;
            impulseForces.Clear();
            impulseLife.Clear();
            body.MovePosition(transform.position);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            var p = transform.position;
            Gizmos.DrawWireSphere(p + Vector3.up * radius, radius);
            Gizmos.DrawWireSphere(p + Vector3.up * (height - radius), radius);
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(p + Vector3.up * radius, Vector3.down * groundSnap);
        }

        public bool IsMovingOnSlope()
        {
            return Ground.grounded && Ground.angle > slopeLimit * 0.6f;
        }

        public void Teleport(Vector3 position, bool keepVelocity)
        {
            transform.position = position;
            body.MovePosition(position);
            if (!keepVelocity)
            {
                planarVelocity = Vector3.zero;
                verticalVelocity = 0f;
            }
        }
    }
}
