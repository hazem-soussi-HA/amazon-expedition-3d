using System.Collections.Generic;
using UnityEngine;

namespace AmazonExpedition.Environment
{
    [DisallowMultipleComponent]
    public sealed class GrassInteractionManager : MonoBehaviour
    {
        [Header("Bend Sources")]
        [SerializeField] private string bendArrayName = "_GrassBendArray";
        [SerializeField] private int maxPoints = 8;
        [SerializeField] private float radius = 4.5f;
        [SerializeField] private float falloff = 2.2f;

        [Header("Material Scan")]
        [SerializeField] private bool autoFindMaterials = true;
        [SerializeField] private Material[] overrideMaterials;
        [SerializeField] private string bendKeyword = "_GRASS_BEND_ON";

        public static GrassInteractionManager Instance { get; private set; }

        private readonly List<BendPoint> points = new List<BendPoint>();
        private readonly List<Material> materials = new List<Material>();
        private Vector4[] upload;
        private bool pendingRebuild;

        private struct BendPoint
        {
            public Vector3 position;
            public float strength;
            public Vector3 velocity;
            public float lastUsed;
        }

        private void Awake()
        {
            Instance = this;
            upload = new Vector4[maxPoints];
            ScanMaterials();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void ScanMaterials()
        {
            materials.Clear();
            if (!autoFindMaterials && overrideMaterials != null)
            {
                for (var i = 0; i < overrideMaterials.Length; i++)
                    if (overrideMaterials[i] != null) materials.Add(overrideMaterials[i]);
                return;
            }

            var renderers = Object.FindObjectsOfType<Renderer>();
            for (var i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;
                var mats = r.sharedMaterials;
                if (mats == null) continue;
                for (var m = 0; m < mats.Length; m++)
                {
                    if (mats[m] == null || materials.Contains(mats[m])) continue;
                    if (!HasBendProperty(mats[m])) continue;
                    materials.Add(mats[m]);
                }
            }
        }

        private static bool HasBendProperty(Material material)
        {
            if (material == null || material.shader == null) return false;
            return material.shader.name.IndexOf("Amazon", System.StringComparison.OrdinalIgnoreCase) >= 0
                || material.shader.name.IndexOf("Grass", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static void PushBend(Vector3 position, float strength, Vector3 velocity)
        {
            if (Instance == null) return;
            Instance.Push(position, strength, velocity);
        }

        private void Push(Vector3 position, float strength, Vector3 velocity)
        {
            var nearestIndex = -1;
            var nearestDistance = float.MaxValue;

            for (var i = 0; i < points.Count; i++)
            {
                var p = points[i];
                var distance = Vector3.Distance(p.position, position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestIndex = i;
                }
            }

            if (points.Count < maxPoints)
            {
                points.Add(new BendPoint { position = position, strength = strength, velocity = velocity, lastUsed = Time.time });
                return;
            }

            if (nearestIndex < 0) return;
            var point = points[nearestIndex];
            var blend = 1f / (1f + nearestDistance / Mathf.Max(0.001f, radius));
            point.position = Vector3.Lerp(point.position, position, blend);
            point.strength = Mathf.Max(point.strength, strength);
            point.velocity = Vector3.Lerp(point.velocity, velocity, blend);
            point.lastUsed = Time.time;
            points[nearestIndex] = point;
        }

        private void Update()
        {
            if (pendingRebuild)
            {
                ScanMaterials();
                pendingRebuild = false;
            }

            var dt = Time.deltaTime;
            for (var i = 0; i < points.Count; i++)
            {
                var point = points[i];
                point.strength -= dt * falloff * 0.5f;
                if (point.strength <= 0f) point.strength = 0f;
                points[i] = point;
            }

            for (var i = 0; i < points.Count; i++)
            {
                var point = points[i];
                var slot = i % maxPoints;
                upload[slot] = new Vector4(point.position.x, point.position.y, point.position.z, point.strength);
            }

            if (upload.Length == 0) return;

            for (var i = 0; i < materials.Count; i++)
            {
                if (materials[i] == null) continue;
                materials[i].SetVectorArray(bendArrayName, upload);
                if (upload.Length > 0 && materials[i].shader != null)
                    materials[i].EnableKeyword(bendKeyword);
            }
        }

        [ContextMenu("Rescan grass materials")]
        public void Rescan()
        {
            pendingRebuild = true;
        }
    }
}
