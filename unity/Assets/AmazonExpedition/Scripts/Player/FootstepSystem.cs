using System.Collections.Generic;
using AmazonExpedition.Environment;
using UnityEngine;

namespace AmazonExpedition.Player
{
    public interface IFootstepReceiver
    {
        void OnFootstep(Vector3 position, Vector3 normal, float strength);
    }

    [DisallowMultipleComponent]
    public sealed class FootstepSystem : MonoBehaviour, IFootstepReceiver
    {
        [Header("Sources")]
        [SerializeField] private Transform leftFootBone;
        [SerializeField] private Transform rightFootBone;
        [SerializeField] private LocomotionDriver locomotion;
        [SerializeField] private PlayerMotor motor;

        [Header("Feedback")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private ParticleSystem footfallDust;
        [SerializeField] private ParticleSystem footfallGrass;
        [SerializeField] private float maxVolumeScale = 1.2f;
        [SerializeField, Range(0f, 1f)] public float globalIntensity = 1f;
        public bool IsFootstepTrigger;

        [Header("Footprints")]
        [SerializeField] private int poolSize = 64;
        [SerializeField] private float footprintLifetime = 45f;
        [SerializeField] private float footprintOffset = 0.012f;
        [SerializeField] private float footprintSize = 0.22f;

        private readonly Queue<Footprint> pool = new Queue<Footprint>();
        private readonly List<Footprint> active = new List<Footprint>();
        private LocomotionDriver subscribedLocomotion;
        private IFootstepReceiver[] receivers;
        private SurfaceProfile lastSurface;

        private sealed class Footprint
        {
            public Transform transform;
            public Renderer renderer;
            public float bornAt;
            public bool inUse;
        }

        private void Awake()
        {
            if (locomotion == null) locomotion = GetComponentInParent<LocomotionDriver>();
            if (motor == null) motor = GetComponentInParent<PlayerMotor>();
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            BuildPool();
        }

        private void OnEnable()
        {
            if (locomotion != null)
            {
                locomotion.Footstep += HandleFootstep;
                subscribedLocomotion = locomotion;
            }
            receivers = GetComponentsInChildren<IFootstepReceiver>();
        }

        private void OnDisable()
        {
            if (subscribedLocomotion != null)
            {
                subscribedLocomotion.Footstep -= HandleFootstep;
                subscribedLocomotion = null;
            }
        }

        public void OnFootstep(Vector3 position, Vector3 normal, float strength)
        {
            HandleFootstep(strength, position, normal);
        }

        private void HandleFootstep(float strength)
        {
            var foot = (leftFootBone != null && Random.value > 0.5f) ? leftFootBone : rightFootBone;
            if (foot == null) foot = transform;
            HandleFootstep(strength, foot.position, Vector3.up);
        }

        private void HandleFootstep(float strength, Vector3 position, Vector3 normal)
        {
            if (motor == null) return;
            var surface = motor.Ground.surface;
            if (surface == null) return;
            lastSurface = surface;

            var volume = strength * maxVolumeScale * globalIntensity;
            if (audioSource != null && surface.footstepClips != null && surface.footstepClips.Length > 0)
            {
                var clip = surface.footstepClips[Random.Range(0, surface.footstepClips.Length)];
                audioSource.PlayOneShot(clip, surface.footstepVolume * volume * Random.Range(0.85f, 1.15f));
            }

            if (surface.grassClippingAmount > 0f && footfallGrass != null)
            {
                footfallGrass.transform.position = position;
                footfallGrass.Emit(Mathf.CeilToInt(surface.grassClippingAmount * volume * 12f));
            }

            if (footfallDust != null)
            {
                footfallDust.transform.position = position;
                footfallDust.Emit(Mathf.CeilToInt(6f * volume));
            }

            if (surface.footstepDustVfx != null)
                Object.Destroy(Object.Instantiate(surface.footstepDustVfx, position, Quaternion.identity), 3f);

            SpawnFootprint(surface, position, normal);

            if (receivers == null) return;
            for (var i = 0; i < receivers.Length; i++)
            {
                if (receivers[i] != null) receivers[i].OnFootstep(position, normal, strength);
            }
        }

        private void BuildPool()
        {
            for (var i = 0; i < poolSize; i++)
            {
                var quad = new GameObject("Footprint");
                quad.transform.SetParent(transform, false);
                var renderer = quad.GetComponent<Renderer>();
                if (renderer == null) renderer = quad.AddComponent<MeshRenderer>();
                quad.SetActive(false);
                var entry = new Footprint { transform = quad.transform, renderer = renderer, bornAt = -1000f, inUse = false };
                pool.Enqueue(entry);
            }
        }

        private void SpawnFootprint(SurfaceProfile surface, Vector3 position, Vector3 normal)
        {
            if (pool.Count == 0) return;
            var entry = pool.Dequeue();

            if (entry.renderer != null)
            {
                var material = entry.renderer.sharedMaterial;
                if (material == null)
                {
                    material = new Material(Shader.Find("Standard")) { mainTexture = surface.footprintTexture };
                    entry.renderer.sharedMaterial = material;
                }
                else
                {
                    material.mainTexture = surface.footprintTexture;
                }
                if (surface.footprintStrength > 0f)
                {
                    material.color = new Color(1f, 1f, 1f, Mathf.Clamp01(surface.footprintStrength));
                }
            }

            var up = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up;
            var orientation = Quaternion.FromToRotation(Vector3.up, up) * Quaternion.Euler(0f, 90f, 0f);
            entry.transform.position = position + up * footprintOffset;
            entry.transform.rotation = orientation;
            entry.transform.localScale = Vector3.one * footprintSize;
            entry.transform.gameObject.SetActive(true);
            entry.bornAt = Time.time;
            entry.inUse = true;
            active.Add(entry);
        }

        private void Update()
        {
            if (active.Count == 0) return;
            for (var i = active.Count - 1; i >= 0; i--)
            {
                var entry = active[i];
                if (Time.time - entry.bornAt < footprintLifetime) continue;
                entry.inUse = false;
                if (entry.transform != null) entry.transform.gameObject.SetActive(false);
                active.RemoveAt(i);
                pool.Enqueue(entry);
            }
        }

        public SurfaceProfile LastSurface { get { return lastSurface; } }
    }
}
