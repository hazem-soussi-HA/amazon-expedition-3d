using UnityEngine;

namespace AmazonExpedition.Environment
{
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    public sealed class SlipSurface : MonoBehaviour
    {
        [Tooltip("Slip multiplier applied on top of the surface profile slipperiness.")]
        [Range(0f, 2f)] public float slipperiness = 0.6f;
        [Tooltip("World-space slide direction. Zero = along the surface normal tangent.")]
        public Vector3 slideDirection;
        [Tooltip("Lateral force applied to anything sliding here.")]
        public float slideForce = 4.5f;
        [Tooltip("If set, the player must be moving at least this fast to slip.")]
        public float minSlipSpeed = 3.5f;

        private void OnTriggerStay(Collider other)
        {
            if (slipForce == 0f) return;
            var motor = other.GetComponent<AmazonExpedition.Player.PlayerMotor>();
            if (motor == null || motor.Speed < minSlipSpeed) return;

            var dir = slideDirection.sqrMagnitude > 0.001f ? slideDirection.normalized : motor.PlanarVelocity.normalized;
            motor.AddImpulse(dir * slideForce, 0.5f);
        }

        private void OnTriggerEnter(Collider other)
        {
            var feet = other.GetComponent<AmazonExpedition.Player.FootstepSystem>();
            if (feet == null) return;
            var surface = GetComponent<SurfaceTag>();
            if (surface == null || surface.profile == slipperinessProfile) return;
            slipForce = surface.profile.slipperiness * slideForce;
        }

        private float slipForce;
        private SurfaceProfile slipperinessProfile;
    }
}
