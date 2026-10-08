using UnityEngine;

namespace AmazonExpedition.Environment
{
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    public sealed class WaterVolume : MonoBehaviour
    {
        [Header("Surface")]
        [SerializeField] private float surfaceHeightWorld;
        [SerializeField] private bool autoHeight = true;
        [SerializeField] private Transform surfaceTransform;

        [Header("Feel")]
        [Range(0f, 1f)] public float currentStrength = 0.25f;
        public Vector3 currentDirection = new Vector3(0f, 0f, -1f);

        private Collider trigger;
        private static WaterVolume primary;

        public static WaterVolume Primary { get { return primary; } }

        private void Awake()
        {
            trigger = GetComponent<Collider>();
            trigger.isTrigger = true;
            if (primary == null) primary = this;
        }

        private void OnDestroy()
        {
            if (primary == this) primary = null;
        }

        public float SampleSurfaceHeight()
        {
            if (autoHeight)
            {
                var t = surfaceTransform != null ? surfaceTransform : transform;
                return t.position.y;
            }
            return surfaceHeightWorld;
        }

        public static float SampleDepth(Vector3 worldPosition, float bodyHeight)
        {
            var volume = primary;
            if (volume == null) return 0f;
            var surface = volume.SampleSurfaceHeight();
            var depth = surface - worldPosition.y;
            return Mathf.Clamp(depth, 0f, bodyHeight);
        }

        public static Vector3 SampleCurrent(Vector3 worldPosition)
        {
            var volume = primary;
            if (volume == null) return Vector3.zero;
            var dir = volume.currentDirection.sqrMagnitude > 0.001f ? volume.currentDirection.normalized : Vector3.forward;
            var falloff = Mathf.Clamp01(volume.SampleSurfaceHeight() - worldPosition.y);
            return dir * (volume.currentStrength * falloff);
        }
    }
}
