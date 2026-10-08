using UnityEngine;

namespace AmazonExpedition.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerMotor))]
    public sealed class PlayerCharacter : MonoBehaviour
    {
        [Header("Modules")]
        [SerializeField] private LocomotionDriver locomotion;
        [SerializeField] private FootPlacementIK footIK;
        [SerializeField] private ProceduralJointBending jointBending;
        [SerializeField] private FootstepSystem footsteps;
        [SerializeField] private PlayerInputSource inputSource;
        [SerializeField] private PlayerCameraRig cameraRig;

        [Header("Stumble Surface Handling")]
        [SerializeField] private float stumbleCooldown = 1.6f;

        private PlayerMotor motor;
        private float lastStumble;

        public PlayerMotor Motor { get { return motor; } }
        public LocomotionDriver Locomotion { get { return locomotion; } }
        public FootstepSystem Footsteps { get { return footsteps; } }
        public bool Alive { get { return true; } }

        private void Awake()
        {
            motor = GetComponent<PlayerMotor>();
            if (locomotion == null) locomotion = GetComponent<LocomotionDriver>();
            if (footIK == null) footIK = GetComponent<FootPlacementIK>();
            if (jointBending == null) jointBending = GetComponent<ProceduralJointBending>();
            if (footsteps == null) footsteps = GetComponent<FootstepSystem>();
            if (inputSource == null) inputSource = GetComponent<PlayerInputSource>();
            if (cameraRig == null) cameraRig = GetComponentInChildren<PlayerCameraRig>();

            if (motor != null && locomotion != null) locomotion.Pose.crouch = 0f;
        }

        private void Update()
        {
            if (motor == null) return;

            if (inputSource != null)
            {
                motor.SetInput(inputSource.Move, inputSource.Jump, inputSource.Sprint);
                if (inputSource.Jump) motor.RequestJump();
                if (cameraRig != null) cameraRig.SetLookDelta(inputSource.LookDelta);
            }
            else
            {
                motor.SetInput(Vector2.zero, false, false);
            }

            EvaluateStumble();
        }

        private void FixedUpdate()
        {
            if (motor == null || jointBending == null) return;
            if (!motor.Grounded && locomotion != null && locomotion.Pose.landImpact > 0.05f)
                jointBending.KickLanding(locomotion.Pose.landImpact);
        }

        private void EvaluateStumble()
        {
            if (motor == null) return;
            var surface = motor.Ground.surface;
            if (surface == null) return;
            if (Time.time - lastStumble < stumbleCooldown) return;
            if (!motor.Grounded) return;

            var overSpeed = motor.Speed > surface.stumbleThreshold;
            if (!overSpeed) return;

            var chance = surface.stumbleChancePerSecond * Time.deltaTime;
            if (Random.value > chance) return;

            lastStumble = Time.time;
            var lateral = Random.insideUnitSphere;
            lateral.y = 0f;
            motor.AddImpulse(lateral.normalized * 3.5f, 0.5f);
        }
    }
}
