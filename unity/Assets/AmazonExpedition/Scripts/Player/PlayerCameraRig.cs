using UnityEngine;

namespace AmazonExpedition.Player
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class PlayerCameraRig : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private Transform head;
        [SerializeField] private Transform yawRoot;
        [SerializeField] private Transform pitchRoot;

        [Header("Rig")]
        [SerializeField] private float eyeHeight = 1.62f;
        [SerializeField] private float followSharpness = 18f;
        [SerializeField] private float lookSensitivity = 2.2f;
        [SerializeField] private float minPitch = -80f;
        [SerializeField] private float maxPitch = 80f;
        [SerializeField] private bool smoothPosition = true;

        [Header("Head Bob")]
        [SerializeField] private float bobFrequency = 1.4f;
        [SerializeField] private float bobAmplitude = 0.035f;
        [SerializeField] private float bobRoll = 0.9f;
        [SerializeField] private bool bobTracksFootsteps = true;

        [Header("Lean & Fov")]
        [SerializeField] private float maxLean = 4f;
        [SerializeField] private float leanSpeed = 6f;
        [SerializeField] private float sprintFovAdd = 8f;
        [SerializeField] private float fovSpeed = 6f;
        [SerializeField] private Camera cameraTarget;

        [Header("Collision")]
        [SerializeField] private float probeRadius = 0.22f;
        [SerializeField] private LayerMask collisionMask = ~0;
        [SerializeField] private float minDistance = 0.4f;

        private float yaw;
        private float pitch;
        private float lean;
        private float bobPhase;
        private float baseFov;
        private LocomotionDriver locomotion;
        private FootstepSystem footstep;

        public float Yaw { get { return yaw; } }
        public float Pitch { get { return pitch; } }

        private void Awake()
        {
            if (motor == null) motor = GetComponentInParent<PlayerMotor>();
            locomotion = GetComponentInParent<LocomotionDriver>();
            subscribedLocomotion = locomotion;
            footstep = GetComponentInParent<FootstepSystem>();
            if (cameraTarget == null) cameraTarget = GetComponentInChildren<Camera>();
            baseFov = cameraTarget != null ? cameraTarget.fieldOfView : 60f;
        }

        private void OnEnable()
        {
            if (locomotion != null) locomotion.Footstep += OnFootstep;
        }

        private void OnDisable()
        {
            if (locomotion != null) locomotion.Footstep -= OnFootstep;
        }

        [SerializeField] private LocomotionDriver subscribedLocomotion;

        private void OnDestroy()
        {
            if (subscribedLocomotion != null) subscribedLocomotion.Footstep -= OnFootstep;
        }

        public void SetLookDelta(Vector2 delta)
        {
            yaw += delta.x * lookSensitivity * 0.01f * 60f * Time.deltaTime;
            pitch -= delta.y * lookSensitivity * 0.01f * 60f * Time.deltaTime;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }

        private void OnFootstep(float strength)
        {
            if (!bobTracksFootsteps) return;
            bobPhase += strength * bobRoll;
        }

        private void LateUpdate()
        {
            if (motor == null) return;
            var dt = Time.deltaTime;

            var headPoint = ResolveHeadPoint();

            if (yawRoot != null) yawRoot.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (pitchRoot != null) pitchRoot.localRotation = Quaternion.Euler(pitch, 0f, 0f);

            if (smoothPosition && head != null)
            {
                head.position = Vector3.Lerp(head.position, headPoint, Mathf.Clamp01(dt * followSharpness));
            }

            var lateral = 0f;
            var speed = motor.Speed;
            if (locomotion != null) lateral = locomotion.Pose.weightShift.x;
            lean = Mathf.Lerp(lean, Mathf.Clamp(-lateral * 2f, -1f, 1f) * maxLean, Mathf.Clamp01(dt * leanSpeed));
            var roll = lean + Mathf.Sin(bobPhase) * bobAmplitude * 0.5f;
            var pitchBob = bobTracksFootsteps ? Mathf.Abs(Mathf.Sin(bobPhase)) * bobAmplitude : 0f;
            if (pitchRoot != null)
                pitchRoot.localRotation = Quaternion.Euler(pitch + pitchBob * Mathf.Rad2Deg, 0f, roll);

            if (cameraTarget != null)
            {
                var fovTarget = baseFov + (motor.Sprinting ? sprintFovAdd : 0f) + Mathf.Min(6f, speed * 0.2f);
                cameraTarget.fieldOfView = Mathf.Lerp(cameraTarget.fieldOfView, fovTarget, Mathf.Clamp01(dt * fovSpeed));
            }

            if (!bobTracksFootsteps) bobPhase += dt * bobFrequency * (0.5f + Mathf.Clamp01(speed / motor.SprintSpeed));
        }

        private Vector3 ResolveHeadPoint()
        {
            var origin = motor.Position + Vector3.up * eyeHeight;
            var dir = yawRoot != null ? yawRoot.forward : transform.forward;
            if (Physics.SphereCast(origin, probeRadius, dir, out RaycastHit hit, minDistance, collisionMask, QueryTriggerInteraction.Ignore))
                return hit.point + hit.normal * probeRadius;
            return origin;
        }
    }
}
