// Headless physics + frame loop for the UnityEngine stub.
//
// The AmazonExpedition port leans hard on Physics: SphereCast ground probes,
// CapsuleCast sweeps, CheckSphere, Raycast, OverlapSphere. This implements
// those against a small world of colliders (analytic heightfield ground plus
// box/sphere obstacles), which is enough to run the motor and the teammate AI
// for real and print honest numbers.

using System;
using System.Collections.Generic;
using System.Reflection;

namespace UnityEngine
{
    /// Heightfield used for the jungle floor. The expedition's terrain function
    /// is ported verbatim from amazon_3d.html so the C# build walks the same map.
    public static class JungleTerrain
    {
        public const float RIVER_Z1 = 10f;
        public const float RIVER_Z2 = 28f;
        public const float WORLD = 210f;
        public const float WATER_LEVEL = -0.55f;

        public static float Height(float x, float z)
        {
            float h = 3f * Mathf.Sin(x * 0.02f) * Mathf.Cos(z * 0.02f)
                    + 1.6f * Mathf.Sin(x * 0.06f + 2f) * Mathf.Sin(z * 0.05f)
                    + 0.7f * Mathf.Sin(x * 0.13f) * Mathf.Cos(z * 0.11f);

            if (z > RIVER_Z1 - 4f && z < RIVER_Z2 + 4f)
            {
                if (z > RIVER_Z1 && z < RIVER_Z2)
                {
                    var d = Mathf.Min(1f, Mathf.Min(z - RIVER_Z1, RIVER_Z2 - z) / 6f + 0.15f);
                    h = -2.6f * d + h * 0.15f;
                }
                else
                {
                    var t = Mathf.Min(z - (RIVER_Z1 - 4f), (RIVER_Z2 + 4f) - z) / 4f;
                    h = h * t - 0.4f * t;
                }
            }

            var dr = Mathf.Sqrt(x * x + (z + 120f) * (z + 120f));
            if (dr < 26f) h = h * (dr / 26f) + 0.5f * (1f - dr / 26f);

            return h;
        }

        /// The browser build does not collide against the analytic function: it
        /// bakes it into a 110x110 vertex grid and walks on the interpolated
        /// surface. Sampling the same grid keeps the C# port on the terrain the
        /// player actually saw, and removes the artificial cliffs the raw
        /// formula puts at the riverbank.
        public const float MESH_SEGMENTS = 110f;
        public const float MESH_CELL = (WORLD * 2f) / MESH_SEGMENTS;

        public static float MeshHeight(float x, float z)
        {
            var half = WORLD;
            var fx = (x + half) / MESH_CELL;
            var fz = (z + half) / MESH_CELL;
            fx = Mathf.Clamp(fx, 0f, MESH_SEGMENTS);
            fz = Mathf.Clamp(fz, 0f, MESH_SEGMENTS);

            var x0 = Mathf.FloorToInt(fx);
            var z0 = Mathf.FloorToInt(fz);
            var x1 = Mathf.Min(x0 + 1, (int)MESH_SEGMENTS);
            var z1 = Mathf.Min(z0 + 1, (int)MESH_SEGMENTS);
            var tx = fx - x0;
            var tz = fz - z0;

            var h00 = Height(-half + x0 * MESH_CELL, -half + z0 * MESH_CELL);
            var h10 = Height(-half + x1 * MESH_CELL, -half + z0 * MESH_CELL);
            var h01 = Height(-half + x0 * MESH_CELL, -half + z1 * MESH_CELL);
            var h11 = Height(-half + x1 * MESH_CELL, -half + z1 * MESH_CELL);

            var a = h00 + (h10 - h00) * tx;
            var b = h01 + (h11 - h01) * tx;
            return a + (b - a) * tz;
        }

        public static Vector3 MeshNormal(float x, float z)
        {
            const float e = 0.2f;
            var hl = MeshHeight(x - e, z);
            var hr = MeshHeight(x + e, z);
            var hd = MeshHeight(x, z - e);
            var hu = MeshHeight(x, z + e);
            return new Vector3(hl - hr, 2f * e, hd - hu).normalized;
        }

        public static Vector3 Normal(float x, float z)
        {
            const float e = 0.35f;
            var hl = Height(x - e, z);
            var hr = Height(x + e, z);
            var hd = Height(x, z - e);
            var hu = Height(x, z + e);
            return new Vector3(hl - hr, 2f * e, hd - hu).normalized;
        }

        public static bool InRiver(float z) { return z > RIVER_Z1 - 3f && z < RIVER_Z2 + 3f; }
        public static bool NearRuins(float x, float z) { return Mathf.Sqrt(x * x + (z + 120f) * (z + 120f)) < 30f; }
        public static bool NearStart(float x, float z) { return Mathf.Sqrt(x * x + (z - 180f) * (z - 180f)) < 14f; }
    }

    public enum ShapeKind { Heightfield, Box, Sphere, Capsule }

    public class PhysShape
    {
        public ShapeKind Kind;
        public Collider Owner;
        public Vector3 Center;
        public Vector3 Size;
        public float Radius;
        public float Height;
        public int Layer = 0;
        public bool IsTrigger;

        public void Bounds(out Vector3 min, out Vector3 max)
        {
            switch (Kind)
            {
                case ShapeKind.Box:
                    var ext = Size * 0.5f;
                    min = Center - ext;
                    max = Center + ext;
                    break;
                case ShapeKind.Sphere:
                    min = Center - new Vector3(Radius, Radius, Radius);
                    max = Center + new Vector3(Radius, Radius, Radius);
                    break;
                case ShapeKind.Capsule:
                    var h = Mathf.Max(Height, Radius * 2f) * 0.5f - Radius;
                    min = Center - new Vector3(Radius, h + Radius, Radius);
                    max = Center + new Vector3(Radius, h + Radius, Radius);
                    break;
                default:
                    min = new Vector3(-4000f, -4000f, -4000f);
                    max = new Vector3(4000f, 4000f, 4000f);
                    break;
            }
        }

        public bool Contains(Vector3 p)
        {
            switch (Kind)
            {
                case ShapeKind.Box:
                {
                    var ext = Size * 0.5f;
                    return Mathf.Abs(p.x - Center.x) <= ext.x
                        && Mathf.Abs(p.y - Center.y) <= ext.y
                        && Mathf.Abs(p.z - Center.z) <= ext.z;
                }
                case ShapeKind.Sphere:
                    return (p - Center).sqrMagnitude <= Radius * Radius;
                case ShapeKind.Capsule:
                {
                    var h = Mathf.Max(Height, Radius * 2f) * 0.5f - Radius;
                    var a = Center + Vector3.up * h;
                    var b = Center - Vector3.up * h;
                    var ab = b - a;
                    var t = Vector3.Dot(p - a, ab) / Mathf.Max(1E-06f, ab.sqrMagnitude);
                    t = Mathf.Clamp01(t);
                    return (p - (a + ab * t)).sqrMagnitude <= Radius * Radius;
                }
                default:
                    return p.y <= JungleTerrain.MeshHeight(p.x, p.z);
            }
        }

        /// Closest point on/in the shape. Used by sweep resolution.
        public Vector3 Closest(Vector3 p)
        {
            switch (Kind)
            {
                case ShapeKind.Box:
                {
                    var ext = Size * 0.5f;
                    return new Vector3(
                        Mathf.Clamp(p.x, Center.x - ext.x, Center.x + ext.x),
                        Mathf.Clamp(p.y, Center.y - ext.y, Center.y + ext.y),
                        Mathf.Clamp(p.z, Center.z - ext.z, Center.z + ext.z));
                }
                case ShapeKind.Sphere:
                {
                    var d = p - Center;
                    var m = d.magnitude;
                    return m < 1E-06f ? Center : Center + d / m * Radius;
                }
                case ShapeKind.Capsule:
                {
                    var h = Mathf.Max(Height, Radius * 2f) * 0.5f - Radius;
                    var a = Center + Vector3.up * h;
                    var b = Center - Vector3.up * h;
                    var ab = b - a;
                    var t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1E-06f, ab.sqrMagnitude));
                    var spine = a + ab * t;
                    var d = p - spine;
                    var m = d.magnitude;
                    return m < 1E-06f ? spine : spine + d / m * Radius;
                }
                default:
                    return new Vector3(p.x, JungleTerrain.MeshHeight(p.x, p.z), p.z);
            }
        }
    }

    /// Static world of shapes plus the query API. Registered once at boot.
    public static class PhysicsEngine
    {
        public static readonly List<PhysShape> Shapes = new List<PhysShape>();
        public static bool UseHeightfield = true;
        public static float InverseFixedDelta = 50f;

        /// Resolved pose from the last MoveCharacter call.
        public static Vector3 LastCharacterPosition;

        /// How far the terrain may intrude into a capsule sweep before it counts
        /// as a hit. Small enough to stay well under the motor's 0.42 m step.
        /// The motor's skin width. The capsule sweep treats ground within this of the
        /// capsule as rest rather than obstruction, matching how a PhysX
        /// character controller slides along the surface it stands on.
        public const float GroundSkinWidth = 0.05f;

        public static int RaycastCount;
        public static int CapsuleCastCount;
        public static int SphereCastCount;

        public static void Reset()
        {
            Shapes.Clear();
            RaycastCount = 0;
            CapsuleCastCount = 0;
            SphereCastCount = 0;
        }

        public static PhysShape AddShape(PhysShape s) { Shapes.Add(s); return s; }

        public static PhysShape GroundShape()
        {
            return new PhysShape { Kind = ShapeKind.Heightfield, Owner = null };
        }

        private static bool LayerOk(int mask, int layer)
        {
            if (mask == 0) return false;
            return (mask & (1 << layer)) != 0;
        }

        private static bool Candidates(List<PhysShape> into, int mask, QueryTriggerInteraction qti)
        {
            into.Clear();
            if (UseHeightfield && LayerOk(mask, 0)) into.Add(GroundShape());
            foreach (var s in Shapes)
            {
                if (!LayerOk(mask, s.Layer)) continue;
                if (s.IsTrigger && qti == QueryTriggerInteraction.Ignore) continue;
                if (!s.IsTrigger && qti == QueryTriggerInteraction.UseGlobal) { /* still collides */ }
                into.Add(s);
            }
            return into.Count > 0;
        }

        private static readonly List<PhysShape> Scratch = new List<PhysShape>();

        private static float ShapeDistance(PhysShape s, Vector3 p)
        {
            var c = s.Closest(p);
            if (s.Kind != ShapeKind.Heightfield && s.Contains(p)) return -1f; // inside
            return (p - c).magnitude;
        }

        // -------------------------------------------------------- Raycast

        public static bool Ray(Vector3 origin, Vector3 direction, float maxDistance,
                               int mask, QueryTriggerInteraction qti, out RaycastHit hit)
        {
            hit = null;
            RaycastCount++;
            if (!Candidates(Scratch, mask, qti)) return false;

            var dir = direction.normalized;
            if (dir.sqrMagnitude < 1E-10f) return false;

            var best = maxDistance;
            var found = false;
            Vector3 bestPoint = origin;
            Vector3 bestNormal = -dir;

            foreach (var s in Scratch)
            {
                // Broadphase: skip shapes whose bounds the segment cannot reach.
                if (s.Kind != ShapeKind.Heightfield)
                {
                    Vector3 sMin, sMax;
                    s.Bounds(out sMin, out sMax);
                    var reach = origin + dir * Mathf.Min(maxDistance, best + 8f);
                    var lo3 = Vector3.Min(origin, reach);
                    var hi3 = Vector3.Max(origin, reach);
                    if (sMax.x < lo3.x - 1f || sMin.x > hi3.x + 1f) continue;
                    if (sMax.y < lo3.y - 1f || sMin.y > hi3.y + 1f) continue;
                    if (sMax.z < lo3.z - 1f || sMin.z > hi3.z + 1f) continue;
                }

                if (s.Kind == ShapeKind.Heightfield)
                {
                    var hitPoint = origin;
                    var hitNormal = -dir;
                    if (RaycastHeightfield(origin, dir, best, ref hitPoint, ref hitNormal))
                    {
                        var d = (hitPoint - origin).magnitude;
                        if (d < best)
                        {
                            best = d;
                            bestPoint = hitPoint;
                            bestNormal = hitNormal;
                            found = true;
                        }
                    }
                    continue;
                }

                Vector3 bmin, bmax;
                s.Bounds(out bmin, out bmax);
                var t = RayBox(origin, dir, bmin, bmax, best);
                if (t >= 0f && t <= best)
                {
                    var p = origin + dir * t;
                    var c = s.Closest(p);
                    var nrm = (p - c).normalized;
                    if (nrm.sqrMagnitude < 1E-10f) nrm = -dir;
                    bestPoint = p;
                    bestNormal = nrm;
                    best = t;
                    found = true;
                }
            }

            if (!found) return false;
            hit = new RaycastHit { point = bestPoint, normal = bestNormal, distance = best, collider = GroundShape().Owner };
            return true;
        }

        private static float RayBox(Vector3 o, Vector3 d, Vector3 bmin, Vector3 bmax, float maxT)
        {
            float tmin = 0f, tmax = maxT;
            for (var a = 0; a < 3; a++)
            {
                var od = o[a];
                var dd = d[a];
                var lo = bmin[a];
                var hi = bmax[a];
                if (Mathf.Abs(dd) < 1E-08f)
                {
                    if (od < lo || od > hi) return -1f;
                    continue;
                }
                float inv = 1f / dd;
                float t1 = (lo - od) * inv;
                float t2 = (hi - od) * inv;
                if (t1 > t2) { var tmp = t1; t1 = t2; t2 = tmp; }
                if (t1 > tmin) tmin = t1;
                if (t2 < tmax) tmax = t2;
                if (tmin > tmax) return -1f;
            }
            return tmin;
        }

        private static bool RaycastHeightfield(Vector3 o, Vector3 d, float maxDist, ref Vector3 point, ref Vector3 normal)
        {
            // March with a step fine enough for the ground relief, then bisect.
            var step = 0.12f;
            var t = 0f;
            while (t < maxDist)
            {
                var p = o + d * t;
                if (p.y <= JungleTerrain.MeshHeight(p.x, p.z))
                {
                    var lo = Mathf.Max(0f, t - step);
                    var hi = t;
                    for (var i = 0; i < 24; i++)
                    {
                        var mid = (lo + hi) * 0.5f;
                        var pm = o + d * mid;
                        if (pm.y <= JungleTerrain.MeshHeight(pm.x, pm.z)) hi = mid; else lo = mid;
                    }
                    point = o + d * hi;
                    normal = JungleTerrain.MeshNormal(point.x, point.z);
                    return true;
                }
                step = Mathf.Min(0.5f, step * 1.06f);
                t += step;
            }
            return false;
        }

        // -------------------------------------------------------- SphereCast

        public static bool SphereCast(Vector3 origin, float radius, Vector3 direction, float maxDistance,
                                      int mask, QueryTriggerInteraction qti, out RaycastHit hit)
        {
            hit = null;
            SphereCastCount++;
            var dir = direction.normalized;
            if (dir.sqrMagnitude < 1E-10f) return false;

            var step = Mathf.Max(0.02f, radius * 0.5f);
            var t = 0f;
            while (t <= maxDistance)
            {
                var p = origin + dir * t;
                if (SphereOverlap(p, radius, mask, qti, out var nearest, out var normal))
                {
                    // Refine back to the contact point by shrinking t.
                    var lo = Mathf.Max(0f, t - step);
                    var hi = t;
                    for (var i = 0; i < 20; i++)
                    {
                        var mid = (lo + hi) * 0.5f;
                        if (SphereOverlap(origin + dir * mid, radius, mask, qti, out _, out _)) hi = mid; else lo = mid;
                    }
                    var hp = origin + dir * hi;
                    SphereOverlap(hp, radius, mask, qti, out _, out var hn);
                    hit = new RaycastHit { point = hp, normal = hn, distance = hi, collider = nearest };
                    return true;
                }
                t += step;
            }
            return false;
        }

        private static bool SphereOverlap(Vector3 center, float radius, int mask, QueryTriggerInteraction qti,
                                          out Collider hitCollider, out Vector3 normal)
        {
            hitCollider = null;
            normal = Vector3.up;
            var best = float.MaxValue;

            if (UseHeightfield && LayerOk(mask, 0))
            {
                var ground = JungleTerrain.MeshHeight(center.x, center.z);
                var d = center.y - radius - ground;
                if (d <= 0f)
                {
                    best = 0f;
                    normal = JungleTerrain.MeshNormal(center.x, center.z);
                }
            }

            foreach (var s in Shapes)
            {
                if (!LayerOk(mask, s.Layer)) continue;
                if (s.IsTrigger && qti == QueryTriggerInteraction.Ignore) continue;
                var c = s.Closest(center);
                var dist = (center - c).magnitude;
                if (dist <= radius && dist < best)
                {
                    best = dist;
                    normal = dist > 1E-05f ? (center - c) / dist : Vector3.up;
                    hitCollider = s.Owner;
                }
            }

            if (best == float.MaxValue) return false;
            if (hitCollider == null)
            {
                // Ground contact: synthesize a collider so callers can read it.
                hitCollider = GroundColliderInstance();
            }
            return true;
        }

        private static Collider _groundCollider;
        private static Collider GroundColliderInstance()
        {
            if (_groundCollider == null)
            {
                _groundCollider = new BoxCollider { name = "TerrainGround" };
                _groundCollider.OwnerForStub();
            }
            return _groundCollider;
        }

        // -------------------------------------------------------- CapsuleCast

        public static bool CapsuleCast(Vector3 point1, Vector3 point2, float radius, Vector3 direction,
                                       float maxDistance, int mask, QueryTriggerInteraction qti, out RaycastHit hit)
        {
            hit = null;
            CapsuleCastCount++;
            var dir = direction.normalized;
            if (dir.sqrMagnitude < 1E-10f) return false;

            // Surfaces the capsule already touches at t=0 are skipped inside the overlap
            // test, matching Unity: a cast does not report colliders it already
            // overlaps. Without this the motor's sweep sees the ground underfoot
            // and never advances; without per-shape handling it would also fail
            // to slide along a wall.
            var step = Mathf.Max(0.02f, radius * 0.45f);
            var t = 0f;
            while (t <= maxDistance)
            {
                var a = point1 + dir * t;
                var b = point2 + dir * t;
                if (CapsuleOverlap(a, b, radius, mask, qti, out var nearest, out var normal))
                {
                    var lo = Mathf.Max(0f, t - step);
                    var hi = t;
                    for (var i = 0; i < 20; i++)
                    {
                        var mid = (lo + hi) * 0.5f;
                        if (CapsuleOverlap(point1 + dir * mid, point2 + dir * mid, radius, mask, qti, out _, out _)) hi = mid; else lo = mid;
                    }
                    var pa = point1 + dir * hi;
                    var pb = point2 + dir * hi;
                    CapsuleOverlap(pa, pb, radius, mask, qti, out _, out var hn);
                    // Contact point is the projection onto the surface, not the
                    // capsule centre: PlayerMotor.TryStepUp derives the step
                    // height from hit.point.y, so a centre-relative point would
                    // read a full body height and reject every step.
                    hit = new RaycastHit
                    {
                        point = SurfacePoint(nearest, pa, pb, hn, radius),
                        normal = hn,
                        distance = hi,
                        collider = nearest
                    };
                    return true;
                }
                t += step;
            }
            return false;
        }

        private static bool CapsuleOverlap(Vector3 a, Vector3 b, float radius, int mask, QueryTriggerInteraction qti,
                                           out Collider hitCollider, out Vector3 normal)
        {
            hitCollider = null;
            normal = Vector3.up;
            var found = false;
            var bestDepth = float.MaxValue;

            // Distance from the capsule axis to the surface; the capsule overlaps
            // when that is under the radius. Tested analytically per shape rather
            // than by tapping spheres, which would report the walking pose as
            // already penetrating the ground.
            if (UseHeightfield && LayerOk(mask, 0))
            {
                // Terrain is a collider like any other. Standing on it is
                // handled by IsStartContact (the capsule bottom grazes the
                // surface it is resting on, so the sweep does not self-collide);
                // terrain rising into the capsule ahead of it does block, which
                // is what lets the motor step up a bank instead of tunnelling
                // into the hillside.
                var closestY = float.MaxValue;
                var sampleX = 0f;
                var sampleZ = 0f;
                for (var i = 0; i <= 6; i++)
                {
                    var p = Vector3.Lerp(a, b, i / 6f);
                    var gap = p.y - radius - JungleTerrain.MeshHeight(p.x, p.z);
                    if (gap < closestY) { closestY = gap; sampleX = p.x; sampleZ = p.z; }
                }
                if (closestY <= 0f)
                {
                    found = true;
                    bestDepth = closestY;
                    hitCollider = GroundColliderInstance();
                    normal = JungleTerrain.MeshNormal(sampleX, sampleZ);
                }
            }

            foreach (var s in Shapes)
            {
                if (!LayerOk(mask, s.Layer)) continue;
                if (s.IsTrigger && qti == QueryTriggerInteraction.Ignore) continue;
                if (!NearAxis(s, a, b, radius + 0.05f)) continue;
                if (IsStartContact(s, a, b, radius)) continue;

                var surface = AxisDistance(s, a, b);
                if (surface >= radius) continue;

                var depth = surface;
                if (s.Contains(Vector3.Lerp(a, b, 0.5f))) depth = -0.001f;
                if (depth >= bestDepth) continue;

                bestDepth = depth;
                found = true;
                hitCollider = s.Owner;
                var mid = Vector3.Lerp(a, b, 0.5f);
                normal = (mid - s.Closest(mid)).normalized;
                if (normal.sqrMagnitude < 1E-10f) normal = Vector3.up;
            }

            return found;
        }

        /// True when the capsule already touches this shape at the start of the sweep.
        /// Unity's casts ignore colliders the shape already overlaps, and the
        /// motor depends on it: without skipping them the capsule sweep sees the
        /// ground underfoot every frame and the body never advances. Per-shape,
        /// so the body still slides along a wall it is already resting against.
        private static bool IsStartContact(PhysShape s, Vector3 a, Vector3 b, float radius)
        {
            if (s.Kind == ShapeKind.Heightfield)
            {
                // Standing counts as contact: the capsule bottom rests on the
                // surface, so the sweep must not report the ground underfoot as
                // an obstruction. Measured with the skin width the motor already
                // carries, so a couple of centimetres of slope is still travel.
                var groundRadius = radius - GroundSkinWidth;
                for (var i = 0; i <= 6; i++)
                {
                    var p = Vector3.Lerp(a, b, i / 6f);
                    if (p.y - groundRadius <= JungleTerrain.MeshHeight(p.x, p.z)) return true;
                }
                return false;
            }
            return AxisDistance(s, a, b) < radius;
        }

        /// Point on the hit surface nearest the capsule axis.
        private static Vector3 SurfacePoint(Collider collider, Vector3 a, Vector3 b, Vector3 normal, float radius)
        {
            var shape = FindShape(collider);
            var mid = Vector3.Lerp(a, b, 0.5f);
            if (shape == null) return mid - normal * radius;

            var best = mid;
            var bestDist = float.MaxValue;
            for (var i = 0; i <= 8; i++)
            {
                var p = Vector3.Lerp(a, b, i / 8f);
                var s = shape.Closest(p);
                var d = (p - s).magnitude;
                if (d < bestDist) { bestDist = d; best = s; }
            }
            return best;
        }

        private static PhysShape FindShape(Collider collider)
        {
            foreach (var s in Shapes) if (ReferenceEquals(s.Owner, collider)) return s;
            return null;
        }

        /// Cheap bounds rejection for a capsule around segment a-b.
        private static bool NearAxis(PhysShape s, Vector3 a, Vector3 b, float pad)
        {
            Vector3 bmin, bmax;
            s.Bounds(out bmin, out bmax);
            var lo = Vector3.Min(a, b) - new Vector3(pad, pad, pad);
            var hi = Vector3.Max(a, b) + new Vector3(pad, pad, pad);
            return !(bmax.x < lo.x || bmin.x > hi.x
                  || bmax.y < lo.y || bmin.y > hi.y
                  || bmax.z < lo.z || bmin.z > hi.z);
        }

        /// Shortest distance from the segment a-b to the shape's surface.
        private static float AxisDistance(PhysShape s, Vector3 a, Vector3 b)
        {
            var closest = float.MaxValue;
            for (var i = 0; i <= 8; i++)
            {
                var p = Vector3.Lerp(a, b, i / 8f);
                var d = s.Closest(p);
                var dist = (p - d).magnitude;
                if (dist < closest) closest = dist;
            }
            return closest;
        }

        // -------------------------------------------------------- Overlap queries

        public static bool CheckSphere(Vector3 center, float radius, int mask, QueryTriggerInteraction qti)
        {
            return SphereOverlap(center, radius, mask, qti, out _, out _);
        }

        public static Collider[] OverlapSphere(Vector3 center, float radius, int mask, QueryTriggerInteraction qti)
        {
            var results = new List<Collider>();
            foreach (var s in Shapes)
            {
                if (!LayerOk(mask, s.Layer)) continue;
                if (s.IsTrigger && qti == QueryTriggerInteraction.Ignore) continue;

                // NearAxis measures to the bounds, so a large trigger zone whose
                // origin is far away still reports correctly. Closest() alone
                // would miss the same zone.
                if (!NearAxis(s, center, center, radius)) continue;

                var c = s.Closest(center);
                if ((center - c).magnitude <= radius && s.Owner != null) results.Add(s.Owner);
            }
            return results.ToArray();
        }

        // -------------------------------------------------------- CharacterController.Move

        public static CollisionFlags MoveCharacter(CharacterController cc, Vector3 from, Vector3 motion)
        {
            var target = from + motion;
            var mask = ~0;
            var radius = cc.radius;
            var halfHeight = Mathf.Max(0.01f, cc.height * 0.5f - radius);

            var flags = CollisionFlags.None;

            // Ground probe first, like Unity's own controller.
            var feet = target + Vector3.up * radius;
            var grounded = false;
            Vector3 groundNormal = Vector3.up;
            if (SphereCast(feet + Vector3.up * 0.05f, radius, Vector3.down, radius + 0.25f, mask, QueryTriggerInteraction.Ignore, out var gh))
            {
                grounded = true;
                groundNormal = gh.normal;
                if (Vector3.Angle(gh.normal, Vector3.up) <= cc.slopeLimit) target = gh.point + Vector3.up * radius;
                else flags |= CollisionFlags.Sides;
            }
            cc.isGrounded = grounded && motion.y <= 0.001f;
            if (cc.isGrounded) flags |= CollisionFlags.Below;

            // Horizontal blocking + step-up.
            var planar = new Vector3(motion.x, 0f, motion.z);
            if (planar.sqrMagnitude > 1E-08f)
            {
                var p1 = target + Vector3.up * (halfHeight + radius * 0.5f);
                var p2 = target + Vector3.up * (halfHeight + radius * 1.5f);
                if (CapsuleCast(p1, p2, radius, planar.normalized, planar.magnitude, mask, QueryTriggerInteraction.Ignore, out var block))
                {
                    flags |= CollisionFlags.Sides;
                    var travel = Mathf.Max(0f, block.distance - radius * 0.05f);
                    target += planar.normalized * travel;

                    // Attempt a step up onto the obstruction (roots, rocks, ruins steps).
                    var lift = block.normal.sqrMagnitude > 1E-06f ? block.normal.y : 0f;
                    if (lift > 0.2f)
                    {
                        var stepHeight = Mathf.Min(cc.stepOffset, block.point.y - from.y);
                        if (stepHeight > 0.01f)
                        {
                            var up = target + Vector3.up * stepHeight;
                            var down = up;
                            if (SphereCast(down + Vector3.up * (halfHeight + radius), radius, Vector3.down,
                                            stepHeight + radius + 0.3f, mask, QueryTriggerInteraction.Ignore, out var land))
                            {
                                target = new Vector3(target.x, land.point.y + radius + 0.001f, target.z);
                            }
                        }
                    }
                }
            }

            target.y += motion.y;
            LastCharacterPosition = target;
            return flags;
        }
    }

    /// Public Physics facade matching the engine API.
    public static class Physics
    {
        public static bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit hitInfo, float maxDistance,
                                   int layerMask, QueryTriggerInteraction qti)
        {
            return PhysicsEngine.Ray(origin, direction, maxDistance, layerMask, qti, out hitInfo);
        }

        public static bool Raycast(Vector3 origin, Vector3 direction, float maxDistance, int layerMask, QueryTriggerInteraction qti)
        {
            RaycastHit h;
            return PhysicsEngine.Ray(origin, direction, maxDistance, layerMask, qti, out h);
        }

        public static bool Raycast(Vector3 origin, Vector3 direction, float maxDistance, int layerMask,
                                   QueryTriggerInteraction qti, out RaycastHit hitInfo)
        {
            return PhysicsEngine.Ray(origin, direction, maxDistance, layerMask, qti, out hitInfo);
        }

        public static bool SphereCast(Vector3 origin, float radius, Vector3 direction, out RaycastHit hitInfo,
                                      float maxDistance, int layerMask, QueryTriggerInteraction qti)
        {
            return PhysicsEngine.SphereCast(origin, radius, direction, maxDistance, layerMask, qti, out hitInfo);
        }

        public static bool CapsuleCast(Vector3 point1, Vector3 point2, float radius, Vector3 direction, out RaycastHit hitInfo,
                                       float maxDistance, int layerMask, QueryTriggerInteraction qti)
        {
            return PhysicsEngine.CapsuleCast(point1, point2, radius, direction, maxDistance, layerMask, qti, out hitInfo);
        }

        public static bool CheckSphere(Vector3 position, float radius, int layerMask, QueryTriggerInteraction qti)
        {
            return PhysicsEngine.CheckSphere(position, radius, layerMask, qti);
        }

        public static Collider[] OverlapSphere(Vector3 position, float radius, int layerMask, QueryTriggerInteraction qti)
        {
            return PhysicsEngine.OverlapSphere(position, radius, layerMask, qti);
        }
    }

    /// Drives Awake/Update/FixedUpdate/LateUpdate across the scene, in the
    /// execution order Unity would use.
    public static class FrameLoop
    {
        public struct Step
        {
            public string Method;
            public int Order;
            public int AddOrder;
            public MonoBehaviour Target;
        }

        private static List<Step> Steps;
        private static List<MonoBehaviour> Behaviours;

        public static void Rebuild()
        {
            Steps = new List<Step>();
            Behaviours = new List<MonoBehaviour>();
            var addOrder = 0;
            foreach (var go in GameObject.All)
            {
                if (!go.activeInHierarchy) continue;
                foreach (var c in go.Components)
                {
                    var mb = c as MonoBehaviour;
                    if (mb != null) Behaviours.Add(mb);
                }
            }

            foreach (var mb in Behaviours)
            {
                var t = mb.GetType();
                var order = 0;
                var attrs = t.GetCustomAttributes(typeof(DefaultExecutionOrder), false);
                if (attrs.Length > 0) order = ((DefaultExecutionOrder)attrs[0]).order;

                foreach (var name in new[] { "Update", "FixedUpdate", "LateUpdate" })
                    Steps.Add(new Step { Method = name, Order = order, AddOrder = addOrder, Target = mb });

                addOrder++;
            }

            Steps.Sort((x, y) =>
            {
                if (x.Method != y.Method) return FrameRank(x.Method).CompareTo(FrameRank(y.Method));
                if (x.Order != y.Order) return x.Order.CompareTo(y.Order);
                return x.AddOrder.CompareTo(y.AddOrder);
            });
        }

        public static void DumpOrder()
        {
            foreach (var s in Steps)
                if (s.Method == "Update")
                    Console.WriteLine("   {0}  <- {1}", s.Order, s.Target.GetType().Name);
        }

        private static int FrameRank(string method)
        {
            switch (method)
            {
                case "Update": return 0;
                case "FixedUpdate": return 1;
                default: return 2;
            }
        }

        /// Unity guarantees Awake before any Update on the same object, and OnEnable
        /// runs before the first Update of any enabled object.
        public static void WakeAll()
        {
            foreach (var mb in Behaviours) Lifecycle.Invoke(mb, "Awake");
            foreach (var mb in Behaviours)
                if (!(mb is Behaviour) || ((Behaviour)mb).enabled) Lifecycle.Invoke(mb, "OnEnable");
        }

        public static void TickFrame()
        {
            foreach (var s in Steps)
                if (s.Method == "Update")
                    if (!(s.Target is Behaviour b) || b.enabled) Lifecycle.Invoke(s.Target, "Update");
        }

        public static void TickFixed()
        {
            foreach (var s in Steps)
                if (s.Method == "FixedUpdate")
                    if (!(s.Target is Behaviour b) || b.enabled) Lifecycle.Invoke(s.Target, "FixedUpdate");
        }

        public static void TickLate()
        {
            foreach (var s in Steps)
                if (s.Method == "LateUpdate")
                    if (!(s.Target is Behaviour b) || b.enabled) Lifecycle.Invoke(s.Target, "LateUpdate");
        }
    }
}

namespace UnityEngine
{
    internal static class StubExtensions
    {
        /// Lets the physics layer hand back a synthetic collider without a
        /// GameObject backing it.
        internal static void OwnerForStub(this Collider c)
        {
            var go = new GameObject(c.name);
            go.Components.Add(c);
            c.owner = go;
        }
    }
}