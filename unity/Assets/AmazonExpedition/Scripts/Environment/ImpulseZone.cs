using UnityEngine;

namespace AmazonExpedition.Environment
{
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    public sealed class ImpulseZone : MonoBehaviour
    {
        [Tooltip("Impulse direction is derived from the contact direction towards the player if null.")]
        public Vector3 impulseDirection;
        [Range(0f, 25f)] public float impulseStrength = 7f;
        [Tooltip("If true the zone fires once per object.")]
        public bool oncePerActor = true;

        private void Reset()
        {
            var col = GetComponent<Collider>();
            col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            Apply(other, true);
        }

        private void OnTriggerStay(Collider other)
        {
            if (oncePerActor) return;
            Apply(other, false);
        }

        private void Apply(Collider other, bool entering)
        {
            var motor = other.GetComponent<AmazonExpedition.Player.PlayerMotor>();
            if (motor == null) return;
            if (oncePerActor && !entering) return;

            Vector3 dir;
            if (impulseDirection.sqrMagnitude > 0.001f)
                dir = impulseDirection.normalized;
            else
                dir = (motor.Position - transform.position);
            dir.y = Mathf.Abs(dir.y) * 0.35f + 0.25f;

            motor.AddImpulse(dir.normalized * impulseStrength, 0.7f);
        }
    }
}
