using UnityEngine;

namespace AmazonExpedition.Environment
{
    [CreateAssetMenu(menuName = "Amazon Expedition/Surface Profile", fileName = "SurfaceProfile")]
    public sealed class SurfaceProfile : ScriptableObject
    {
        [Header("Identity")]
        public string surfaceName = "New Surface";
        [Tooltip("Used by the footprint system to pick a decal texture.")]
        public Texture2D footprintTexture;

        [Header("Locomotion Physics")]
        [Tooltip("Multiplier on acceleration while grounded here. 1 = normal ground.")]
        [Range(0.05f, 2f)] public float traction = 1f;
        [Tooltip("Velocity damping per second. Sand, mud and snow slow you down.")]
        [Range(0f, 30f)] public float drag = 0f;
        [Tooltip("How far the body slides along this surface when moving fast.")]
        [Range(0f, 1f)] public float slipperiness = 0f;
        [Tooltip("Speed above which traversing this surface can cause a stumble.")]
        [Range(0f, 20f)] public float stumbleThreshold = 8f;
        [Tooltip("Chance per second of stumbling while above the threshold.")]
        [Range(0f, 1f)] public float stumbleChancePerSecond = 0f;
        [Tooltip("Vertical sink into the surface, in metres. Mud displaces.")]
        [Range(0f, 0.4f)] public float sinkDepth = 0f;

        [Header("Feedback")]
        public AudioClip[] footstepClips;
        [Range(0f, 1.5f)] public float footstepVolume = 1f;
        public GameObject footstepDustVfx;
        [Range(0f, 1f)] public float footprintStrength = 1f;
        [Range(0f, 20f)] public float grassClippingAmount = 0f;
    }
}
