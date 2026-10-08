using UnityEngine;

namespace AmazonExpedition.Environment
{
    [DisallowMultipleComponent]
    public sealed class GrassContact : MonoBehaviour
    {
        [Range(0.1f, 3f)] [SerializeField] private float strength = 1f;
        [SerializeField] private float pushInterval = 0.25f;

        private AmazonExpedition.Player.PlayerMotor motor;
        private float nextPush;

        private void Awake()
        {
            motor = GetComponentInParent<AmazonExpedition.Player.PlayerMotor>();
        }

        private void Update()
        {
            if (motor == null) return;
            if (Time.time < nextPush) return;
            if (motor.Speed < 0.4f) return;
            nextPush = Time.time + pushInterval;
            GrassInteractionManager.PushBend(motor.Position, strength * Mathf.Clamp01(motor.Speed / motor.JogSpeed), motor.PlanarVelocity);
        }
    }
}
