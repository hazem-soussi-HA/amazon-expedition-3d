using UnityEngine;

namespace AmazonExpedition.Player
{
    [RequireComponent(typeof(Animator))]
    [DisallowMultipleComponent]
    public sealed class FootPlacementIK : MonoBehaviour
    {
        [Header("Bones")]
        [SerializeField] private Animator animator;
        [SerializeField] private Transform leftFoot;
        [SerializeField] private Transform rightFoot;
        [SerializeField] private Transform hips;

        [Header("Ground")]
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField] private float castUp = 0.6f;
        [SerializeField] private float castDown = 1.6f;
        [SerializeField] private float footRadius = 0.14f;
        [SerializeField] private float pelvisOffsetMultiplier = 0.45f;
        [SerializeField] private float footPitchRange = 35f;
        [SerializeField] private float footRollRange = 25f;
        [SerializeField] private bool alignToGround = true;

        [Header("Blending")]
        [Range(1f, 30f)] [SerializeField] private float response = 14f;
        [Range(0f, 1f)] [SerializeField] private float lockWhenMoving = 1f;

        private Vector3 smoothLeft = Vector3.zero;
        private Vector3 smoothRight = Vector3.zero;
        private Quaternion smoothLeftRot = Quaternion.identity;
        private Quaternion smoothRightRot = Quaternion.identity;
        private float leftWeight;
        private float rightWeight;
        private float pelvisOffset;
        private float pelvisOffsetSmooth;

        public float LeftWeight { get { return leftWeight; } }
        public float RightWeight { get { return rightWeight; } }
        public float PelvisOffset { get { return pelvisOffset; } }

        private void Reset()
        {
            animator = GetComponent<Animator>();
        }

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            ResolveBones();
        }

        private void ResolveBones()
        {
            if (animator == null) return;
            if (leftFoot == null) leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            if (rightFoot == null) rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            if (hips == null) hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        }

        private Vector3 FootOrigin(Transform foot, out Quaternion footRot)
        {
            footRot = foot.rotation;
            return foot.position + foot.up * 0.04f;
        }

        private bool CastFoot(Vector3 origin, out RaycastHit hit)
        {
            var start = origin + Vector3.up * castUp;
            var distance = castUp + castDown;
            return Physics.Raycast(start, Vector3.down, out hit, distance, groundMask, QueryTriggerInteraction.Ignore);
        }

        private Quaternion AlignToNormal(Quaternion current, Vector3 normal)
        {
            if (!alignToGround) return current;
            var up = Vector3.up;
            var rot = Quaternion.FromToRotation(up, normal);
            var pitched = rot * current;
            var euler = pitched.eulerAngles;
            euler.x = Mathf.DeltaAngle(euler.x, 0f);
            euler.x = Mathf.Clamp(euler.x, -footPitchRange, footPitchRange);
            euler.z = Mathf.Clamp(euler.z > 180f ? euler.z - 360f : euler.z, -footRollRange, footRollRange);
            return Quaternion.Euler(euler);
        }

        private void LateUpdate()
        {
            if (leftFoot == null || rightFoot == null || hips == null) { ResolveBones(); }
            if (leftFoot == null || rightFoot == null) return;

            var dt = Time.deltaTime;
            var blend = dt * response;
            var moving = MovementReference();

            PlaceFoot(leftFoot, ref smoothLeft, ref smoothLeftRot, ref leftWeight, blend, moving, 1);
            PlaceFoot(rightFoot, ref smoothRight, ref smoothRightRot, ref rightWeight, blend, moving, -1);

            var hipsWorld = hips.position;
            var pelvisTarget = Mathf.Min(smoothLeft.y, smoothRight.y);
            pelvisOffset = Mathf.Clamp(pelvisTarget - hipsWorld.y, -0.6f, 0.35f) * pelvisOffsetMultiplier;
            pelvisOffsetSmooth = Mathf.Lerp(pelvisOffsetSmooth, pelvisOffset, blend * 0.6f);
            hips.position = new Vector3(hipsWorld.x, hipsWorld.y + pelvisOffsetSmooth, hipsWorld.z);
        }

        private float MovementReference()
        {
            var motor = GetComponent<PlayerMotor>();
            if (motor != null) return motor.Speed;
            return 0f;
        }

        private void PlaceFoot(Transform foot, ref Vector3 position, ref Quaternion rotation,
                                ref float weight, float blend, float moving, int sideSign)
        {
            var origin = FootOrigin(foot, out var restRotation);
            if (CastFoot(origin, out var hit))
            {
                position = Vector3.Lerp(position, hit.point, blend);
                var aligned = AlignToNormal(restRotation, hit.normal);
                rotation = Quaternion.Slerp(rotation, aligned, blend);
                var targetWeight = Mathf.Clamp01(lockWhenMoving * (moving > 0.05f ? 1f : 0.6f));
                weight = Mathf.MoveTowards(weight, targetWeight, dtWeight);
                foot.position = new Vector3(foot.position.x, position.y + 0.02f, foot.position.z);
                foot.rotation = Quaternion.Slerp(foot.rotation, rotation, blend);
            }
            else
            {
                weight = Mathf.MoveTowards(weight, 0f, dtWeight);
            }
        }

        private float dtWeight;
        private void Update()
        {
            dtWeight = Time.deltaTime * 6f;
        }
    }
}
