using UnityEngine;

namespace AmazonExpedition.Environment
{
    public sealed class SurfaceTag : MonoBehaviour
    {
        [Tooltip("Surface applied to anything standing on this collider.")]
        public SurfaceProfile profile;
    }

    [System.Serializable]
    public sealed class TerrainSurfaceEntry
    {
        public Texture2D splatTexture;
        public SurfaceProfile profile;
    }

    public sealed class TerrainSurfaceResolver : MonoBehaviour
    {
        public static TerrainSurfaceResolver Instance { get; private set; }

        [SerializeField] private Terrain terrain;
        [SerializeField] private TerrainSurfaceEntry[] entries;
        [SerializeField] private SurfaceProfile fallback;

        private void Awake()
        {
            Instance = this;
            if (terrain == null) terrain = GetComponent<Terrain>();
            if (terrain == null) terrain = Terrain.activeTerrain;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public SurfaceProfile Resolve(Vector3 worldPosition, SurfaceProfile currentDefault)
        {
            var result = ResolveTerrain(worldPosition);
            if (result != null) return result;
            var tag = FindTag(worldPosition);
            if (tag != null) return tag.profile;
            return fallback != null ? fallback : currentDefault;
        }

        private SurfaceProfile ResolveTerrain(Vector3 worldPosition)
        {
            if (terrain == null || terrain.terrainData == null || entries == null) return null;

            var data = terrain.terrainData;
            var tp = terrain.GetPosition();
            var local = worldPosition - tp;
            var u = Mathf.Clamp01(local.x / data.size.x);
            var v = Mathf.Clamp01(local.z / data.size.z);

            var ax = Mathf.Clamp(Mathf.FloorToInt(u * data.alphamapWidth), 0, data.alphamapWidth - 1);
            var ay = Mathf.Clamp(Mathf.FloorToInt(v * data.alphamapHeight), 0, data.alphamapHeight - 1);

            float[,,] map;
            try { map = data.GetAlphamaps(ax, ay, 1, 1); }
            catch { return null; }
            if (map == null || map.GetLength(2) == 0) return null;

            var best = 0;
            var bestWeight = -1f;
            var layers = map.GetLength(2);
            for (var i = 0; i < layers; i++)
            {
                if (i >= entries.Length || entries[i] == null || entries[i].profile == null) continue;
                var w = map[0, 0, i];
                if (w > bestWeight) { bestWeight = w; best = i; }
            }

            if (bestWeight <= 0f) return null;
            return entries[best] != null ? entries[best].profile : null;
        }

        private SurfaceTag FindTag(Vector3 worldPosition)
        {
            var hits = Physics.OverlapSphere(worldPosition + Vector3.up * 0.3f, 0.6f, ~0, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < hits.Length; i++)
            {
                var tag = hits[i].GetComponent<SurfaceTag>();
                if (tag != null && tag.profile != null) return tag;
            }
            return null;
        }
    }

    public static class SurfaceResolver
    {
        public static SurfaceProfile Resolve(Vector3 worldPosition, SurfaceProfile fallback, SurfaceProfile cached)
        {
            var resolver = TerrainSurfaceResolver.Instance;
            if (resolver != null) return resolver.Resolve(worldPosition, fallback);
            return cached != null ? cached : fallback;
        }
    }
}
