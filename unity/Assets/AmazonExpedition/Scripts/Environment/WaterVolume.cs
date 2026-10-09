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

        /// True when the point is horizontally inside the water body.
        public bool ContainsPoint(Vector3 worldPosition)
        {
            if (trigger == null) return true;
            var t = trigger.transform;
            var local = t.InverseTransformPoint(worldPosition);
            var box = trigger as BoxCollider;
            if (box != null)
            {
                var half = box.size * 0.5f;
                return Mathf.Abs(local.x) <= half.x
                    && Mathf.Abs(local.z) <= half.z;
            }
            var sphere = trigger as SphereCollider;
            if (sphere != null)
            {
                var centre = t.TransformPoint(sphere.center);
            var horizontal = new Vector3(worldPosition.x - centre.x, 0f, worldPosition.z - centre.z);
            return horizontal.magnitude <= sphere.radius;
            }
            return true;
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

        /// Depth of water at a point, 0 outside the volume. The horizontal bounds come
        /// from the trigger collider, so a player in a valley far downstream of
        /// the river is not reported as submerged just because it sits below the
        /// water plane.
        public static float SampleDepth(Vector3 worldPosition, float bodyHeight)
        {
            var volume = primary;
            if (volume == null) return 0f;
            if (!volume.ContainsPoint(worldPosition)) return 0f;
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
