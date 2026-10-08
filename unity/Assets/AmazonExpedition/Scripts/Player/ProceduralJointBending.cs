using UnityEngine;

namespace AmazonExpedition.Player
{
    [DisallowMultipleComponent]
    public sealed class ProceduralJointBending : MonoBehaviour
    {
        [Header("Bones")]
        [SerializeField] private Animator animator;
        [SerializeField] private Transform spine;
        [SerializeField] private Transform leftKnee;
        [SerializeField] private Transform rightKnee;
        [SerializeField] private Transform leftElbow;
        [SerializeField] private Transform rightElbow;

        [Header("Crouch Spring")]
        [SerializeField] private float crouchStiffness = 120f;
        [SerializeField] private float crouchDamping = 11f;
        [SerializeField] private float maxCrouch = 0.42f;
        [SerializeField] private float landKick = 0.55f;

        [Header("Lean")]
        [Range(0f, 20f)] [SerializeField] private float maxLean = 9f;
        [Range(1f, 30f)] [SerializeField] private float leanSpeed = 8f;
        [SerializeField] private bool leanIntoAcceleration = true;

        [Header("Knee Protection")]
        [SerializeField] private LayerMask collisionMask = ~0;
        [SerializeField] private float kneeProbe = 0.28f;

        private PlayerMotor motor;
        private LocomotionDriver locomotion;
        private float crouchVelocity;
        private float crouch;
        private Quaternion spineBase = Quaternion.identity;
        private bool baseCaptured;

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            motor = GetComponentInParent<PlayerMotor>();
            locomotion = GetComponentInParent<LocomotionDriver>();
            ResolveBones();
            CaptureBasePose();
        }

        public void Rebind()
        {
            ResolveBones();
            CaptureBasePose();
        }

        private void CaptureBasePose()
        {
            if (spine == null) return;
            spineBase = spine.localRotation;
            baseCaptured = true;
        }

        private void ResolveBones()
        {
            if (animator == null) return;
            if (spine == null) spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            if (leftKnee == null) leftKnee = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            if (rightKnee == null) rightKnee = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            if (leftElbow == null) leftElbow = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            if (rightElbow == null) rightElbow = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        }

        private void Update()
        {
            if (motor == null) return;
            var dt = Time.deltaTime;

            var target = 0f;
            if (locomotion != null) target = locomotion.Pose.crouch;
            if (motor.Grounded && motor.Speed > 0.5f) target += motor.IsMovingOnSlope() ? 0.25f : 0f;
            if (motor.InWater) target += Mathf.Clamp01(motor.WaterDepth) * 0.4f;

            var damp = crouchDamping;
            var delta = target - crouch;
            crouchVelocity += (delta * crouchStiffness - crouchVelocity * damp) * dt;
            crouch += crouchVelocity * dt;
            crouch = Mathf.Clamp(crouch, -maxCrouch, maxCrouch);

            ApplyJoints();
        }

        public void KickLanding(float impact)
        {
            crouchVelocity += landKick * impact;
        }

        private void ApplyJoints()
        {
            if (crouch > 0.001f || crouch < -0.001f)
            {
                BendKnee(leftKnee, crouch);
                BendKnee(rightKnee, crouch);
            }

            if (!leanIntoAcceleration || spine == null || !baseCaptured) return;

            var lateral = 0f;
            var forward = 0f;
            if (locomotion != null)
            {
                lateral = Mathf.Clamp(locomotion.Pose.weightShift.x * 3f, -1f, 1f);
                forward = Mathf.Clamp(locomotion.Pose.forwardAccel * 0.6f, -1f, 1f);
            }

            var leanTarget = new Vector3(
                -forward * maxLean * 0.5f,
                0f,
                Mathf.Clamp(-lateral * maxLean, -maxLean, maxLean));
            spine.localRotation = Quaternion.Slerp(spine.localRotation, spineBase * Quaternion.Euler(leanTarget), Time.deltaTime * leanSpeed);
        }

        private void BendKnee(Transform knee, float amount)
        {
            if (knee == null) return;
            var euler = knee.localEulerAngles;
            var current = NormalizeAngle(euler.x);
            var limit = KneeLimit(knee);
            var target = Mathf.Clamp(current - amount * 90f, 0f, limit);
            euler.x = target;
            knee.localEulerAngles = euler;
        }

        private float KneeLimit(Transform knee)
        {
            if (knee == null) return 130f;
            var child = knee.childCount > 0 ? knee.GetChild(0) : null;
            if (child == null) return 130f;
            var direction = (child.position - knee.position).normalized;
            var target = new Vector3(knee.position.x, child.position.y, knee.position.z);
            if (Physics.Raycast(knee.position + Vector3.up * 0.05f, direction, out RaycastHit hit, kneeProbe, collisionMask, QueryTriggerInteraction.Ignore))
            {
                return Mathf.Clamp(Vector3.Angle(-Vector3.up, hit.normal) * 0.8f, 15f, 130f);
            }
            return 130f;
        }

        private static float NormalizeAngle(float angle)
        {
            var a = angle % 360f;
            if (a > 180f) a -= 360f;
            if (a < -180f) a += 360f;
            return a;
        }
    }
}
