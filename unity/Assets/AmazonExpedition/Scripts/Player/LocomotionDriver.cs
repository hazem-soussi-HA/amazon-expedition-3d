using UnityEngine;

namespace AmazonExpedition.Player
{
    public sealed class ProceduralPose
    {
        public Vector2 weightShift;
        public float crouch;
        public float lean;
        public float footPhaseL;
        public float footPhaseR;
        public float lastFootPhase;
        public float strideTimer;
        public float landImpact;
        public float turnRate;
        public float lateralAccel;
        public float forwardAccel;
        public Vector3 expectedFootL;
        public Vector3 expectedFootR;
    }

    public sealed class LocomotionDriver : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private Animator animator;
        [SerializeField] private Transform modelRoot;

        [Header("Animation Parameters")]
        [SerializeField] private string speedParam = "Speed";
        [SerializeField] private string strafeParam = "Strafe";
        [SerializeField] private string verticalParam = "Vertical";
        [SerializeField] private string accelParam = "Acceleration";
        [SerializeField] private string turnParam = "Turn";
        [SerializeField] private string groundedParam = "Grounded";
        [SerializeField] private string slopeParam = "Slope";
        [SerializeField] private string crouchParam = "Crouch";
        [SerializeField] private string waterParam = "WaterDepth";
        [SerializeField] private string landParam = "LandImpact";
        [SerializeField] private string stumbleParam = "Stumble";
        [SerializeField] private string sprintParam = "Sprint";

        [Header("Blending")]
        [Range(0.01f, 0.6f)] [SerializeField] private float paramDamp = 0.08f;
        [Range(0f, 1f)] [SerializeField] private float turnInPlaceGate = 0.3f;

        [Header("Weight Shift")]
        [Range(0f, 0.6f)] [SerializeField] private float maxWeightShift = 0.22f;
        [Range(0.5f, 12f)] [SerializeField] private float weightShiftResponse = 4.5f;
        [SerializeField] private bool useRootMotionFacing = true;

        // Created at construction, not in Awake: PlayerCharacter.Awake reads
        // Pose and Unity gives no ordering guarantee between the two Awakes.
        public ProceduralPose Pose { get; private set; } = new ProceduralPose();

        private int hashSpeed, hashStrafe, hashVertical, hashAccel, hashTurn, hashGrounded,
            hashSlope, hashCrouch, hashWater, hashLand, hashStumble, hashSprint;
        private Vector3 previousPlanar;
        private Vector3 previousPosition;
        private float smoothedSpeed;
        private float smoothedLateral;
        private float smoothedTurn;
        private float smoothedLand;
        private float landCoyote;
        private bool previousGrounded;

        private void Awake()
        {
            if (motor == null) motor = GetComponentInParent<PlayerMotor>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (modelRoot == null && animator != null) modelRoot = animator.transform;
            previousPlanar = motor != null ? motor.PlanarVelocity : Vector3.zero;
            previousPosition = motor != null ? motor.Position : Vector3.zero;
            previousGrounded = motor != null && motor.Grounded;
        }

        private void OnEnable()
        {
            if (animator == null) return;
            hashSpeed = animator.StringToHash(speedParam);
            hashStrafe = animator.StringToHash(strafeParam);
            hashVertical = animator.StringToHash(verticalParam);
            hashAccel = animator.StringToHash(accelParam);
            hashTurn = animator.StringToHash(turnParam);
            hashGrounded = animator.StringToHash(groundedParam);
            hashSlope = animator.StringToHash(slopeParam);
            hashCrouch = animator.StringToHash(crouchParam);
            hashWater = animator.StringToHash(waterParam);
            hashLand = animator.StringToHash(landParam);
            hashStumble = animator.StringToHash(stumbleParam);
            hashSprint = animator.StringToHash(sprintParam);
        }

        public event System.Action<float> Footstep;

        private void Update()
        {
            if (motor == null) return;
            var dt = Time.deltaTime;

            var planar = motor.PlanarVelocity;
            var speed = planar.magnitude;
            var reference = motor.Sprinting ? motor.SprintSpeed : motor.JogSpeed;
            var normalized = Mathf.Clamp01(speed / Mathf.Max(0.001f, reference));

            var worldDelta = motor.Position - previousPosition;
            var localDelta = modelRoot != null
                ? modelRoot.InverseTransformDirection(worldDelta)
                : worldDelta;
            previousPosition = motor.Position;

            var accel = (planar - previousPlanar) / Mathf.Max(0.0001f, dt);
            previousPlanar = planar;

            var forwardAxis = modelRoot != null ? modelRoot.forward : Vector3.forward;
            var rightAxis = modelRoot != null ? modelRoot.right : Vector3.right;
            var localForward = Vector3.Dot(localDelta, forwardAxis);
            var localRight = Vector3.Dot(localDelta, rightAxis);
            var localSign = localForward >= 0f ? 1f : -1f;
            var strafe = localSign * Mathf.Clamp01(Mathf.Abs(localRight) / Mathf.Max(0.001f, reference));
            var vertical = Mathf.Clamp(motor.VerticalVelocity / 6f, -1f, 1f);

            var rightVel = Vector3.Dot(planar, rightAxis);
            var turnRate = Vector3.Dot(new Vector3(accel.x, 0f, accel.z), rightAxis);

            smoothedSpeed = Mathf.Lerp(smoothedSpeed, normalized, dt / paramDamp);
            smoothedLateral = Mathf.MoveTowards(smoothedLateral, strafe, dt * 6f);
            smoothedTurn = Mathf.Lerp(smoothedTurn, Mathf.Clamp(turnRate / 6f, -1f, 1f), dt / paramDamp);

            if (motor.Grounded && !previousGrounded)
            {
                var impact = Mathf.Clamp01(Mathf.Abs(motor.VerticalVelocity) / 14f);
                smoothedLand = impact;
                landCoyote = 0.35f;
                RaiseFootstep(1.6f);
            }
            previousGrounded = motor.Grounded;
            if (landCoyote > 0f) landCoyote -= dt;
            else smoothedLand = Mathf.MoveTowards(smoothedLand, 0f, dt * 3.5f);

            var shiftTarget = new Vector2(
                Mathf.Clamp(-rightVel / Mathf.Max(0.001f, reference) * 1.4f, -1f, 1f),
                Mathf.Clamp(smoothedSpeed * 0.35f, 0f, 1f));
            shiftTarget.x = Mathf.Clamp(shiftTarget.x, -1f, 1f) * maxWeightShift;
            shiftTarget.y *= maxWeightShift * 0.35f;
            Pose.weightShift = Vector2.MoveTowards(Pose.weightShift, shiftTarget, dt * weightShiftResponse * maxWeightShift * 5f);

            var stride = motor.Grounded && speed > 0.1f ? dt * (0.9f + normalized * 1.6f) : 0f;
            Pose.strideTimer += stride;
            RaiseStrideFootsteps(normalized, dt);
            Pose.landImpact = smoothedLand;
            Pose.turnRate = smoothedTurn;
            Pose.lateralAccel = smoothedLateral;
            Pose.forwardAccel = smoothedSpeed;

            if (animator != null && animator.enabled)
            {
                animator.SetFloat(speedParam, normalized, paramDamp, dt);
                animator.SetFloat(strafeParam, smoothedLateral, paramDamp, dt);
                animator.SetFloat(verticalParam, vertical, paramDamp, dt);
                animator.SetFloat(accelParam, Mathf.Clamp01(Vector3.Dot(accel, forwardAxis) / 12f), paramDamp, dt);
                animator.SetFloat(turnParam, smoothedTurn, paramDamp, dt);
                animator.SetBool(hashGrounded, motor.Grounded);
                animator.SetFloat(slopeParam, Mathf.Clamp01(motor.Ground.angle / slopeLimitReference), paramDamp, dt);
                animator.SetFloat(crouchParam, Pose.crouch, paramDamp, dt);
                animator.SetFloat(waterParam, Mathf.Clamp01(motor.WaterDepth / 1.4f), paramDamp, dt);
                animator.SetFloat(landParam, smoothedLand, paramDamp, dt);
                animator.SetFloat(stumbleParam, motor.StumbleShake, paramDamp, dt);
                animator.SetBool(hashSprint, motor.Sprinting);
            }

            RotateModel(dt);
        }

        [Range(10f, 90f)] [SerializeField] private float slopeLimitReference = 50f;

        private void RotateModel(float dt)
        {
            if (modelRoot == null || !useRootMotionFacing) return;
            var planar = motor.PlanarVelocity;
            if (planar.sqrMagnitude < 0.05f) return;

            var target = Quaternion.LookRotation(planar.normalized, Vector3.up);
            var speed = motor.Grounded ? turnResponse : turnResponse * 0.5f;
            modelRoot.rotation = Quaternion.Slerp(modelRoot.rotation, target, Mathf.Clamp01(dt * speed));
        }

        [Range(1f, 30f)] [SerializeField] private float turnResponse = 10f;

        public void RaiseFootstep(float strength)
        {
            var handler = Footstep;
            if (handler != null) handler(strength);
        }

        /// Fires a footstep each time the stride cycle passes through a footfall.
        /// Animation events cover a rigged character, but anything driving the
        /// procedural rig has no events to rely on and would otherwise stay
        /// silent for every step.
        private void RaiseStrideFootsteps(float normalized, float dt)
        {
            if (!motor.Grounded || motor.Speed <= 0.1f) return;

            var cadence = 0.62f - Mathf.Clamp01(normalized) * 0.22f;
            strideAccumulator += dt;
            if (strideAccumulator < cadence) return;
            strideAccumulator -= cadence;

            RaiseFootstep(0.55f + Mathf.Clamp01(normalized) * 0.65f);
        }

        private float strideAccumulator;

        public void AnimationEvent_Footstep(string foot)
        {
            RaiseFootstep(foot == "L" ? 1f : 1f);
        }

        public void AnimationEvent_Land(float strength)
        {
            smoothedLand = strength;
        }

        private void LateUpdate()
        {
            ApplyProceduralPose();
        }

        private void ApplyProceduralPose()
        {
            if (animator == null) return;
            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            if (hips == null) return;
            var local = hips.localPosition;
            hips.localPosition = new Vector3(local.x - Pose.weightShift.x, local.y - Pose.crouch * 0.25f, local.z);
        }
    }
}
