// Headless UnityEngine API stub.
//
// Scope: enough of UnityEngine to compile and EXECUTE the 16 AmazonExpedition
// scripts under Mono/mcs, with no Unity installation and no engine licence.
// This is test scaffolding, not engine code: everything here is a minimal
// implementation of the API surface the port actually touches.
//
// Physics and the frame loop live in UnityPhysics.cs.

using System;
using System.Collections;
using System.Collections.Generic;

namespace UnityEngine
{
    // ------------------------------------------------------------ Mathf

    public static class Mathf
    {
        public const float PI = 3.14159265358979f;
        public const float Infinity = float.PositiveInfinity;
        public const float NegativeInfinity = float.NegativeInfinity;
        public const float Deg2Rad = PI * 2f / 360f;
        public const float Rad2Deg = 360f / (PI * 2f);
        public const float Epsilon = 1.401298E-45f;

        public static float Abs(float v) { return Math.Abs(v); }
        public static int Abs(int v) { return Math.Abs(v); }
        public static float Sqrt(float v) { return (float)Math.Sqrt(v); }
        public static float Sin(float v) { return (float)Math.Sin(v); }
        public static float Cos(float v) { return (float)Math.Cos(v); }
        public static float Tan(float v) { return (float)Math.Tan(v); }
        public static float Acos(float v) { return (float)Math.Acos(Clamp(v, -1f, 1f)); }
        public static float Asin(float v) { return (float)Math.Asin(Clamp(v, -1f, 1f)); }
        public static float Atan(float v) { return (float)Math.Atan(v); }
        public static float Atan2(float y, float x) { return (float)Math.Atan2(y, x); }
        public static float Exp(float v) { return (float)Math.Exp(v); }
        public static float Pow(float a, float b) { return (float)Math.Pow(a, b); }
        public static float Floor(float v) { return (float)Math.Floor(v); }
        public static float Ceil(float v) { return (float)Math.Ceiling(v); }
        public static float Round(float v) { return (float)Math.Round(v, MidpointRounding.ToEven); }
        public static int FloorToInt(float v) { return (int)Math.Floor(v); }
        public static int CeilToInt(float v) { return (int)Math.Ceiling(v); }
        public static int RoundToInt(float v) { return (int)Math.Round(v, MidpointRounding.ToEven); }
        public static float Sign(float v) { return v >= 0f ? 1f : -1f; }
        public static float Min(float a, float b) { return a < b ? a : b; }
        public static int Min(int a, int b) { return a < b ? a : b; }
        public static float Max(float a, float b) { return a > b ? a : b; }
        public static int Max(int a, int b) { return a > b ? a : b; }
        public static float Min(params float[] v) { var r = v[0]; for (var i = 1; i < v.Length; i++) if (v[i] < r) r = v[i]; return r; }
        public static float Max(params float[] v) { var r = v[0]; for (var i = 1; i < v.Length; i++) if (v[i] > r) r = v[i]; return r; }
        public static float Clamp(float v, float lo, float hi) { return v < lo ? lo : (v > hi ? hi : v); }
        public static int Clamp(int v, int lo, int hi) { return v < lo ? lo : (v > hi ? hi : v); }
        public static float Clamp01(float v) { return v < 0f ? 0f : (v > 1f ? 1f : v); }
        public static float Lerp(float a, float b, float t) { return a + (b - a) * Clamp01(t); }
        public static float LerpUnclamped(float a, float b, float t) { return a + (b - a) * t; }
        public static float InverseLerp(float a, float b, float v) { return a == b ? 0f : Clamp01((v - a) / (b - a)); }
        public static float MoveTowards(float cur, float target, float maxDelta)
        {
            if (Abs(target - cur) <= maxDelta) return target;
            return cur + Sign(target - cur) * maxDelta;
        }
        public static float SmoothDamp(float cur, float target, ref float vel, float smoothTime, float maxSpeed = Infinity, float deltaTime = 0.02f)
        {
            smoothTime = Max(0.0001f, smoothTime);
            var omega = 2f / smoothTime;
            var x = omega * deltaTime;
            var exp = 1f / (1f + x + 0.48f * x * x + 0.235f * x * x * x);
            var change = cur - target;
            var original = target;
            var maxChange = maxSpeed * smoothTime;
            change = Clamp(change, -maxChange, maxChange);
            target = cur - change;
            var temp = (vel + omega * change) * deltaTime;
            vel = (vel - omega * temp) * exp;
            var output = target + (change + temp) * exp;
            if (original - cur > 0f == output > original) { output = original; vel = 0f; }
            return output;
        }
        public static float Repeat(float t, float length) { return Clamp(t - Floor(t / length) * length, 0f, length); }
        public static float PingPong(float t, float length)
        {
            t = Repeat(t, length * 2f);
            return length - Abs(t - length);
        }
        public static float DeltaAngle(float current, float target)
        {
            var delta = Repeat(target - current, 360f);
            if (delta > 180f) delta -= 360f;
            return delta;
        }
        public static bool Approximately(float a, float b)
        {
            return Abs(b - a) < Max(1E-06f * Max(Abs(a), Abs(b)), Epsilon * 8f);
        }
    }

    // ------------------------------------------------------------ Vector2

    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }

        public static Vector2 zero { get { return new Vector2(0f, 0f); } }
        public static Vector2 one { get { return new Vector2(1f, 1f); } }
        public static Vector2 up { get { return new Vector2(0f, 1f); } }
        public static Vector2 right { get { return new Vector2(1f, 0f); } }

        public float magnitude { get { return Mathf.Sqrt(x * x + y * y); } }
        public float sqrMagnitude { get { return x * x + y * y; } }
        public Vector2 normalized { get { var m = magnitude; return m > 1E-05f ? this / m : zero; } }

        public void Normalize() { var m = magnitude; if (m > 1E-05f) { x /= m; y /= m; } }

        public static Vector2 operator +(Vector2 a, Vector2 b) { return new Vector2(a.x + b.x, a.y + b.y); }
        public static Vector2 operator -(Vector2 a, Vector2 b) { return new Vector2(a.x - b.x, a.y - b.y); }
        public static Vector2 operator -(Vector2 a) { return new Vector2(-a.x, -a.y); }
        public static Vector2 operator *(Vector2 a, float d) { return new Vector2(a.x * d, a.y * d); }
        public static Vector2 operator *(float d, Vector2 a) { return new Vector2(a.x * d, a.y * d); }
        public static Vector2 operator /(Vector2 a, float d) { return new Vector2(a.x / d, a.y / d); }
        public static bool operator ==(Vector2 a, Vector2 b) { return (a - b).sqrMagnitude < 1E-10f; }
        public static bool operator !=(Vector2 a, Vector2 b) { return !(a == b); }
        public override bool Equals(object o) { return o is Vector2 && this == (Vector2)o; }
        public override int GetHashCode() { return x.GetHashCode() ^ (y.GetHashCode() << 2); }
        public override string ToString() { return "(" + x.ToString("F2") + ", " + y.ToString("F2") + ")"; }

        public float this[int i]
        {
            get { return i == 0 ? x : y; }
            set { if (i == 0) x = value; else y = value; }
        }

        public static float Dot(Vector2 a, Vector2 b) { return a.x * b.x + a.y * b.y; }
        public static float Distance(Vector2 a, Vector2 b) { return (a - b).magnitude; }
        public static Vector2 Lerp(Vector2 a, Vector2 b, float t) { t = Mathf.Clamp01(t); return new Vector2(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t); }
        public static Vector2 LerpUnclamped(Vector2 a, Vector2 b, float t) { return new Vector2(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t); }
        public static Vector2 MoveTowards(Vector2 cur, Vector2 target, float maxDelta)
        {
            var d = target - cur;
            var m = d.magnitude;
            if (m <= maxDelta || m < 1E-05f) return target;
            return cur + d / m * maxDelta;
        }
        public static Vector2 Min(Vector2 a, Vector2 b) { return new Vector2(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y)); }
        public static Vector2 Max(Vector2 a, Vector2 b) { return new Vector2(Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y)); }
        public static Vector2 Scale(Vector2 a, Vector2 b) { return new Vector2(a.x * b.x, a.y * b.y); }
    }

    // ------------------------------------------------------------ Vector3

    public struct Vector3 : IEquatable<Vector3>
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public Vector3(float x, float y) : this(x, y, 0f) { }
        public Vector3(float v) : this(v, v, v) { }

        public static Vector3 zero { get { return new Vector3(0f, 0f, 0f); } }
        public static Vector3 one { get { return new Vector3(1f, 1f, 1f); } }
        public static Vector3 up { get { return new Vector3(0f, 1f, 0f); } }
        public static Vector3 down { get { return new Vector3(0f, -1f, 0f); } }
        public static Vector3 left { get { return new Vector3(-1f, 0f, 0f); } }
        public static Vector3 right { get { return new Vector3(1f, 0f, 0f); } }
        public static Vector3 forward { get { return new Vector3(0f, 0f, 1f); } }
        public static Vector3 back { get { return new Vector3(0f, 0f, -1f); } }

        public float magnitude { get { return Mathf.Sqrt(x * x + y * y + z * z); } }
        public float sqrMagnitude { get { return x * x + y * y + z * z; } }
        public Vector3 normalized
        {
            get { var m = magnitude; return m > 1E-05f ? new Vector3(x / m, y / m, z / m) : zero; }
        }

        public void Normalize() { var n = normalized; x = n.x; y = n.y; z = n.z; }

        public float this[int i]
        {
            get { return i == 0 ? x : (i == 1 ? y : z); }
            set { if (i == 0) x = value; else if (i == 1) y = value; else z = value; }
        }

        public static Vector3 operator +(Vector3 a, Vector3 b) { return new Vector3(a.x + b.x, a.y + b.y, a.z + b.z); }
        public static Vector3 operator -(Vector3 a, Vector3 b) { return new Vector3(a.x - b.x, a.y - b.y, a.z - b.z); }
        public static Vector3 operator -(Vector3 a) { return new Vector3(-a.x, -a.y, -a.z); }
        public static Vector3 operator *(Vector3 a, float d) { return new Vector3(a.x * d, a.y * d, a.z * d); }
        public static Vector3 operator *(float d, Vector3 a) { return new Vector3(a.x * d, a.y * d, a.z * d); }
        public static Vector3 operator /(Vector3 a, float d) { return new Vector3(a.x / d, a.y / d, a.z / d); }
        public static bool operator ==(Vector3 a, Vector3 b) { return (a - b).sqrMagnitude < 1E-10f; }
        public static bool operator !=(Vector3 a, Vector3 b) { return !(a == b); }

        public bool Equals(Vector3 o) { return x == o.x && y == o.y && z == o.z; }
        public override bool Equals(object o) { return o is Vector3 && Equals((Vector3)o); }
        public override int GetHashCode() { return x.GetHashCode() ^ (y.GetHashCode() << 2) ^ (z.GetHashCode() >> 2); }
        public override string ToString() { return "(" + x.ToString("F2") + ", " + y.ToString("F2") + ", " + z.ToString("F2") + ")"; }

        public static float Dot(Vector3 a, Vector3 b) { return a.x * b.x + a.y * b.y + a.z * b.z; }
        public static Vector3 Cross(Vector3 a, Vector3 b)
        {
            return new Vector3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
        }
        public static float Distance(Vector3 a, Vector3 b) { return (a - b).magnitude; }
        public static float Angle(Vector3 from, Vector3 to)
        {
            var denom = from.magnitude * to.magnitude;
            if (denom < 1E-15f) return 0f;
            return Mathf.Acos(Mathf.Clamp(Dot(from, to) / denom, -1f, 1f)) * Mathf.Rad2Deg;
        }
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) { t = Mathf.Clamp01(t); return new Vector3(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t); }
        public static Vector3 LerpUnclamped(Vector3 a, Vector3 b, float t) { return new Vector3(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t); }
        public static Vector3 MoveTowards(Vector3 cur, Vector3 target, float maxDelta)
        {
            var d = target - cur;
            var m = d.magnitude;
            if (m <= maxDelta || m < 1E-05f) return target;
            return cur + d / m * maxDelta;
        }
        public static Vector3 Scale(Vector3 a, Vector3 b) { return new Vector3(a.x * b.x, a.y * b.y, a.z * b.z); }
        public static Vector3 ProjectOnPlane(Vector3 v, Vector3 n)
        {
            var d = Dot(n, v);
            return v - n * d;
        }
        public static Vector3 Project(Vector3 v, Vector3 n) { return n * Dot(n, v); }
        public static Vector3 ClampMagnitude(Vector3 v, float max) { return v.magnitude > max ? v.normalized * max : v; }
        public static Vector3 Min(Vector3 a, Vector3 b) { return new Vector3(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Min(a.z, b.z)); }
        public static Vector3 Max(Vector3 a, Vector3 b) { return new Vector3(Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y), Mathf.Max(a.z, b.z)); }
        public static Vector3 SmoothDamp(Vector3 cur, Vector3 target, ref Vector3 vel, float smoothTime, float maxSpeed = Mathf.Infinity, float deltaTime = 0.02f)
        {
            vel = Vector3.zero;
            return new Vector3(
                Mathf.SmoothDamp(cur.x, target.x, ref vel.x, smoothTime, maxSpeed, deltaTime),
                Mathf.SmoothDamp(cur.y, target.y, ref vel.y, smoothTime, maxSpeed, deltaTime),
                Mathf.SmoothDamp(cur.z, target.z, ref vel.z, smoothTime, maxSpeed, deltaTime));
        }
    }

    // ------------------------------------------------------------ Vector4

    public struct Vector4
    {
        public float x, y, z, w;
        public Vector4(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public Vector4(float x, float y, float z) : this(x, y, z, 0f) { }
        public static Vector4 zero { get { return new Vector4(0f, 0f, 0f, 0f); } }
        public static Vector4 one { get { return new Vector4(1f, 1f, 1f, 1f); } }
        public float magnitude { get { return Mathf.Sqrt(x * x + y * y + z * z + w * w); } }
        public float sqrMagnitude { get { return x * x + y * y + z * z + w * w; } }
        public override string ToString() { return "(" + x + ", " + y + ", " + z + ", " + w + ")"; }
        public static implicit operator Vector4(Vector3 v) { return new Vector4(v.x, v.y, v.z, 0f); }
        public static implicit operator Vector3(Vector4 v) { return new Vector3(v.x, v.y, v.z); }
        public static Vector4 operator +(Vector4 a, Vector4 b) { return new Vector4(a.x + b.x, a.y + b.y, a.z + b.z, a.w + b.w); }
        public static Vector4 operator *(Vector4 a, float d) { return new Vector4(a.x * d, a.y * d, a.z * d, a.w * d); }
    }

    // ------------------------------------------------------------ Quaternion

    public struct Quaternion
    {
        public float x, y, z, w;
        public Quaternion(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }

        public static Quaternion identity { get { return new Quaternion(0f, 0f, 0f, 1f); } }

        public Quaternion normalized
        {
            get
            {
                var m = Mathf.Sqrt(x * x + y * y + z * z + w * w);
                return m < 1E-08f ? identity : new Quaternion(x / m, y / m, z / m, w / m);
            }
        }

        // Unity composes Euler angles in Z, X, Y order.
        public static Quaternion Euler(float xDeg, float yDeg, float zDeg)
        {
            var hx = xDeg * Mathf.Deg2Rad * 0.5f;
            var hy = yDeg * Mathf.Deg2Rad * 0.5f;
            var hz = zDeg * Mathf.Deg2Rad * 0.5f;
            var qx = new Quaternion(Mathf.Sin(hx), 0f, 0f, Mathf.Cos(hx));
            var qy = new Quaternion(0f, Mathf.Sin(hy), 0f, Mathf.Cos(hy));
            var qz = new Quaternion(0f, 0f, Mathf.Sin(hz), Mathf.Cos(hz));
            return qz * qx * qy;
        }

        public static Quaternion Euler(Vector3 e) { return Euler(e.x, e.y, e.z); }

        // Inverse of Euler(x,y,z) == Euler(z) * Euler(x) * Euler(y).
        // Rotation matrix rows R00..R22; extraction below follows that product.
        public Vector3 eulerAngles
        {
            get
            {
                var q = normalized;
                var m01 = 2f * (q.x * q.y - q.z * q.w);
                var m11 = 1f - 2f * (q.x * q.x + q.z * q.z);
                var m20 = 2f * (q.x * q.z - q.y * q.w);
                var m21 = 2f * (q.y * q.z + q.x * q.w);
                var m22 = 1f - 2f * (q.x * q.x + q.y * q.y);

                float ex, ey, ez;
                if (Mathf.Abs(m11) > 0.999999f)
                {
                    ex = Mathf.Asin(Mathf.Clamp(m21, -1f, 1f));
                    ey = Mathf.Atan2(-m20, m22);
                    ez = 0f;
                }
                else
                {
                    ex = Mathf.Asin(Mathf.Clamp(m21, -1f, 1f));
                    ey = Mathf.Atan2(-m20, m22);
                    ez = Mathf.Atan2(-m01, m11);
                }
                return new Vector3(ex, ey, ez) * Mathf.Rad2Deg;
            }
        }

        public static Quaternion AngleAxis(float angleDeg, Vector3 axis)
        {
            var a = axis.normalized;
            var h = angleDeg * Mathf.Deg2Rad * 0.5f;
            var s = Mathf.Sin(h);
            return new Quaternion(a.x * s, a.y * s, a.z * s, Mathf.Cos(h));
        }

        public static Quaternion LookRotation(Vector3 forward) { return LookRotation(forward, Vector3.up); }

        public static Quaternion LookRotation(Vector3 forward, Vector3 up)
        {
            var f = forward.normalized;
            if (f.sqrMagnitude < 1E-10f) return identity;
            var r = Vector3.Cross(up, f).normalized;
            if (r.sqrMagnitude < 1E-10f) r = Vector3.Cross(Vector3.right, f).normalized;
            var u = Vector3.Cross(f, r);
            return FromAxes(r, u, f);
        }

        private static Quaternion FromAxes(Vector3 right, Vector3 up, Vector3 forward)
        {
            var m00 = right.x;
            var m01 = up.x;
            var m02 = forward.x;
            var m10 = right.y;
            var m11 = up.y;
            var m12 = forward.y;
            var m20 = right.z;
            var m21 = up.z;
            var m22 = forward.z;
            float t = m00 + m11 + m22;
            if (t > 0f)
            {
                var s = Mathf.Sqrt(t + 1f) * 2f;
                return new Quaternion((m21 - m12) / s, (m02 - m20) / s, (m10 - m01) / s, 0.25f * s);
            }
            if (m00 > m11 && m00 > m22)
            {
                var s = Mathf.Sqrt(1f + m00 - m11 - m22) * 2f;
                return new Quaternion(0.25f * s, (m01 + m10) / s, (m02 + m20) / s, (m21 - m12) / s);
            }
            if (m11 > m22)
            {
                var s = Mathf.Sqrt(1f + m11 - m00 - m22) * 2f;
                return new Quaternion((m01 + m10) / s, 0.25f * s, (m12 + m21) / s, (m02 - m20) / s);
            }
            var s2 = Mathf.Sqrt(1f + m22 - m00 - m11) * 2f;
            return new Quaternion((m02 + m20) / s2, (m12 + m21) / s2, 0.25f * s2, (m10 - m01) / s2);
        }

        public static Quaternion FromToRotation(Vector3 from, Vector3 to)
        {
            var a = from.normalized;
            var b = to.normalized;
            var d = Vector3.Dot(a, b);
            if (d >= 0.999999f) return identity;
            if (d <= -0.999999f)
            {
                var axis = Vector3.Cross(Vector3.right, a);
                if (axis.sqrMagnitude < 1E-08f) axis = Vector3.Cross(Vector3.up, a);
                return AngleAxis(180f, axis.normalized);
            }
            var c = Vector3.Cross(a, b);
            return new Quaternion(c.x, c.y, c.z, 1f + d).normalized;
        }

        public static float Dot(Quaternion a, Quaternion b) { return a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w; }

        public static Quaternion Inverse(Quaternion r)
        {
            var d = Dot(r, r);
            if (d < 1E-12f) return identity;
            return new Quaternion(-r.x / d, -r.y / d, -r.z / d, r.w / d);
        }

        public static Quaternion Slerp(Quaternion a, Quaternion b, float t)
        {
            t = Mathf.Clamp01(t);
            var dot = Dot(a, b);
            if (dot < 0f) { b = new Quaternion(-b.x, -b.y, -b.z, -b.w); dot = -dot; }
            if (dot > 0.9995f)
            {
                var r = new Quaternion(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t,
                                        a.z + (b.z - a.z) * t, a.w + (b.w - a.w) * t);
                return r.normalized;
            }
            var theta0 = Mathf.Acos(Mathf.Clamp(dot, -1f, 1f));
            var theta = theta0 * t;
            var sin0 = Mathf.Sin(theta0);
            var s0 = Mathf.Cos(theta) - dot * Mathf.Sin(theta) / sin0;
            var s1 = Mathf.Sin(theta) / sin0;
            return new Quaternion(a.x * s0 + b.x * s1, a.y * s0 + b.y * s1,
                                  a.z * s0 + b.z * s1, a.w * s0 + b.w * s1);
        }

        public static Quaternion operator *(Quaternion a, Quaternion b)
        {
            return new Quaternion(
                a.w * b.x + a.x * b.w + a.y * b.z - a.z * b.y,
                a.w * b.y + a.y * b.w + a.z * b.x - a.x * b.z,
                a.w * b.z + a.z * b.w + a.x * b.y - a.y * b.x,
                a.w * b.w - a.x * b.x - a.y * b.y - a.z * b.z);
        }

        public static Vector3 operator *(Quaternion q, Vector3 v)
        {
            var x2 = q.x * 2f;
            var y2 = q.y * 2f;
            var z2 = q.z * 2f;
            var xx = q.x * x2;
            var yy = q.y * y2;
            var zz = q.z * z2;
            var xy = q.x * y2;
            var xz = q.x * z2;
            var yz = q.y * z2;
            var wx = q.w * x2;
            var wy = q.w * y2;
            var wz = q.w * z2;
            return new Vector3(
                (1f - (yy + zz)) * v.x + (xy - wz) * v.y + (xz + wy) * v.z,
                (xy + wz) * v.x + (1f - (xx + zz)) * v.y + (yz - wx) * v.z,
                (xz - wy) * v.x + (yz + wx) * v.y + (1f - (xx + yy)) * v.z);
        }

        public static bool operator ==(Quaternion a, Quaternion b) { return Dot(a, b) > 0.999999f; }
        public static bool operator !=(Quaternion a, Quaternion b) { return !(a == b); }
        public override bool Equals(object o) { return o is Quaternion && this == (Quaternion)o; }
        public override int GetHashCode() { return x.GetHashCode() ^ (y.GetHashCode() << 2) ^ (z.GetHashCode() >> 2) ^ (w.GetHashCode() >> 1); }
        public override string ToString() { return "(" + x.ToString("F3") + ", " + y.ToString("F3") + ", " + z.ToString("F3") + ", " + w.ToString("F3") + ")"; }
    }

    // ------------------------------------------------------------ Color

    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public Color(float r, float g, float b) : this(r, g, b, 1f) { }
        public static Color white { get { return new Color(1f, 1f, 1f, 1f); } }
        public static Color black { get { return new Color(0f, 0f, 0f, 1f); } }
        public static Color clear { get { return new Color(0f, 0f, 0f, 0f); } }
        public static Color yellow { get { return new Color(1f, 0.92f, 0.016f, 1f); } }
        public static Color cyan { get { return new Color(0f, 1f, 1f, 1f); } }
        public static Color green { get { return new Color(0f, 1f, 0f, 1f); } }
        public static Color red { get { return new Color(1f, 0f, 0f, 1f); } }
        public static Color blue { get { return new Color(0f, 0f, 1f, 1f); } }
        public static Color gray { get { return new Color(0.5f, 0.5f, 0.5f, 1f); } }
        public static Color Lerp(Color x, Color y, float t)
        {
            t = Mathf.Clamp01(t);
            return new Color(x.r + (y.r - x.r) * t, x.g + (y.g - x.g) * t, x.b + (y.b - x.b) * t, x.a + (y.a - x.a) * t);
        }
        public override string ToString() { return "RGBA(" + r.ToString("F2") + ", " + g.ToString("F2") + ", " + b.ToString("F2") + ", " + a.ToString("F2") + ")"; }
    }

    // ------------------------------------------------------------ Enums

    public enum KeyCode
    {
        None = 0, Space = 32, E = 101, R = 114,
        A = 97, B = 98, C = 99, D = 100, F = 102, Q = 113, S = 115, W = 119,
        LeftShift = 304, RightShift = 303, LeftControl = 306,
        UpArrow = 273, DownArrow = 274, LeftArrow = 275, RightArrow = 276
    }

    public enum HumanBodyBones
    {
        Hips = 0, Spine = 5, LeftUpperLeg = 1, RightUpperLeg = 2,
        LeftLowerLeg = 3, RightLowerLeg = 4, LeftFoot = 17, RightFoot = 18,
        LeftLowerArm = 13, RightLowerArm = 14
    }

    public enum QueryTriggerInteraction { UseGlobal = 0, Ignore = 1, Collide = 2 }

    [Flags]
    public enum CollisionFlags { None = 0, Sides = 1, Above = 2, Below = 4 }

    public enum RigidbodyInterpolation { None = 0, Interpolate = 1, Extrapolate = 2 }

    [Flags]
    public enum RigidbodyConstraints
    {
        None = 0, FreezePositionX = 2, FreezePositionY = 4, FreezePositionZ = 8,
        FreezeRotationX = 16, FreezeRotationY = 32, FreezeRotationZ = 64,
        FreezeRotation = 112, FreezeAll = 126
    }

    // ------------------------------------------------------------ LayerMask

    [Serializable]
    public struct LayerMask
    {
        public int value;
        public LayerMask(int v) { value = v; }
        public static implicit operator int(LayerMask m) { return m.value; }
        public static implicit operator LayerMask(int v) { return new LayerMask(v); }
        public int Contains(int layer) { return (value & (1 << layer)) != 0 ? 1 : 0; }
    }

    // ------------------------------------------------------------ Attributes

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class RequireComponent : Attribute
    {
        public readonly Type[] types;
        public RequireComponent(Type t) { types = new[] { t }; }
        public RequireComponent(Type a, Type b) { types = new[] { a, b }; }
        public RequireComponent(Type a, Type b, Type c) { types = new[] { a, b, c }; }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class DisallowMultipleComponent : Attribute { }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class DefaultExecutionOrder : Attribute
    {
        public readonly int order;
        public DefaultExecutionOrder(int o) { order = o; }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class SerializeField : Attribute { }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class HeaderAttribute : Attribute
    {
        public readonly string header;
        public HeaderAttribute(string h) { header = h; }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class TooltipAttribute : Attribute
    {
        public readonly string tooltip;
        public TooltipAttribute(string t) { tooltip = t; }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class RangeAttribute : Attribute
    {
        public readonly float min, max;
        public RangeAttribute(float a, float b) { min = a; max = b; }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class ContextMenu : Attribute
    {
        public readonly string name;
        public ContextMenu(string n) { name = n; }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class CreateAssetMenuAttribute : Attribute
    {
        public string menuName;
        public string fileName;
        public int order;
        public CreateAssetMenuAttribute() { }
    }

    // ------------------------------------------------------------ Time / Random / Input

    public static class Time
    {
        public static float time;
        public static float deltaTime = 1f / 60f;
        public static float fixedDeltaTime = 0.02f;
        public static float timeScale = 1f;
        public static int frameCount;

        public static void Reset() { time = 0f; frameCount = 0; }

        internal static void Advance(float dt)
        {
            time += dt;
            frameCount++;
        }
    }

    public static class Random
    {
        private static uint state = 0x1234567u;

        public static void Seed(int s) { state = (uint)(s == 0 ? 0x1234567 : s); }

        private static uint Next()
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return state;
        }

        public static float value { get { return (Next() & 0xFFFFFF) / 16777216f; } }

        public static float Range(float min, float max)
        {
            return min + (max - min) * ((Next() & 0xFFFFFF) / 16777216f);
        }

        public static int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            var span = maxExclusive - minInclusive;
            return minInclusive + (int)(Next() % (uint)span);
        }

        public static Vector3 insideUnitSphere
        {
            get
            {
                float x, y, z, s;
                do { x = Range(-1f, 1f); y = Range(-1f, 1f); z = Range(-1f, 1f); s = x * x + y * y + z * z; }
                while (s > 1f || s < 1E-06f);
                return new Vector3(x, y, z);
            }
        }
    }

    /// Scriptable keyboard/axis state so the harness can drive PlayerInputSource
    /// exactly like a human at the keyboard.
    public static class Input
    {
        private static readonly HashSet<KeyCode> Held = new HashSet<KeyCode>();
        private static readonly HashSet<KeyCode> PressedThisFrame = new HashSet<KeyCode>();
        private static readonly Dictionary<string, float> Axes = new Dictionary<string, float>();

        public static void SetAxis(string name, float value) { Axes[name] = value; }
        public static void SetKey(KeyCode key, bool down)
        {
            if (down) { if (Held.Add(key)) PressedThisFrame.Add(key); }
            else Held.Remove(key);
        }
        public static void TapKey(KeyCode key) { Held.Add(key); PressedThisFrame.Add(key); }
        public static void ReleaseAll() { Held.Clear(); PressedThisFrame.Clear(); Axes.Clear(); }

        public static void EndFrame() { PressedThisFrame.Clear(); }

        public static float GetAxis(string name)
        {
            float v;
            return Axes.TryGetValue(name, out v) ? v : 0f;
        }

        public static float GetAxisRaw(string name) { return GetAxis(name); }
        public static bool GetKey(KeyCode key) { return Held.Contains(key); }
        public static bool GetKeyDown(KeyCode key) { return PressedThisFrame.Contains(key); }
        public static bool GetButton(string name)
        {
            switch (name)
            {
                case "Jump": return Held.Contains(KeyCode.Space);
                case "Sprint": return Held.Contains(KeyCode.LeftShift);
                case "Interact": return Held.Contains(KeyCode.E);
                default: return false;
            }
        }
        public static bool GetButtonDown(string name) { return GetButton(name); }
    }

    public static class Application
    {
        public static bool isPlaying = true;
        public static bool isEditor;
        public static string unityVersion = "2021.3.0f1";
        public static string productName = "Amazon Expedition 3D";
    }

    public static class Debug
    {
        public static void Log(object o) { Console.WriteLine(o); }
        public static void LogWarning(object o) { Console.WriteLine("[warn] " + o); }
        public static void LogError(object o) { Console.WriteLine("[error] " + o); }
        public static void DrawRay(Vector3 a, Vector3 b) { }
        public static void DrawLine(Vector3 a, Vector3 b) { }
    }

    public static class Gizmos
    {
        public static Color color;
        public static void DrawWireSphere(Vector3 c, float r) { }
        public static void DrawRay(Vector3 o, Vector3 d) { }
        public static void DrawLine(Vector3 a, Vector3 b) { }
        public static void DrawCube(Vector3 c, Vector3 s) { }
    }

    // ------------------------------------------------------------ Object graph

    public class Object
    {
        public string name = string.Empty;
        public bool destroyed;

        public static void Destroy(Object o) { if (o != null) o.destroyed = true; }
        public static void Destroy(Object o, float t) { if (o != null) o.destroyed = true; }
        public static void DestroyImmediate(Object o) { if (o != null) o.destroyed = true; }

        public static GameObject Instantiate(GameObject src) { return src; }
        public static GameObject Instantiate(GameObject src, Vector3 pos, Quaternion rot) { return src; }
        public static T Instantiate<T>(T src) where T : Object { return src; }

        public static T[] FindObjectsOfType<T>() where T : Object
        {
            var list = new List<T>();
            foreach (var go in GameObject.All)
                foreach (var c in go.Components)
                    if (c is T && !c.destroyed) list.Add((T)(object)c);
            return list.ToArray();
        }

        public static T FindObjectOfType<T>() where T : Object
        {
            var all = FindObjectsOfType<T>();
            return all.Length > 0 ? all[0] : null;
        }

        public static implicit operator bool(Object o) { return !ReferenceEquals(o, null) && !o.destroyed; }
    }

    public class Component : Object
    {
        internal GameObject owner;
        public GameObject gameObject { get { return owner; } }
        public Transform transform { get { return owner != null ? owner.transform : null; } }

        public T GetComponent<T>() { return owner != null ? owner.GetComponent<T>() : default(T); }
        public T GetComponentInParent<T>() { return owner != null ? owner.GetComponentInParent<T>() : default(T); }
        public T GetComponentInChildren<T>() { return owner != null ? owner.GetComponentInChildren<T>() : default(T); }
        public T[] GetComponentsInChildren<T>() { return owner != null ? owner.GetComponentsInChildren<T>() : new T[0]; }
        public T[] GetComponentsInChildren<T>(bool includeInactive) { return GetComponentsInChildren<T>(); }
        public T GetComponentInChildren<T>(bool includeInactive) { return GetComponentInChildren<T>(); }
    }

    public class Behaviour : Component
    {
        public bool enabled = true;
        public bool isActiveAndEnabled { get { return enabled && gameObject != null && gameObject.activeInHierarchy; } }
    }

    public class MonoBehaviour : Behaviour { }

    public class ScriptableObject : Object
    {
        public static T CreateInstance<T>() where T : ScriptableObject, new()
        {
            var so = new T();
            Lifecycle.Invoke(so, "Awake");
            return so;
        }
    }

    /// Unity calls Awake on all components after the whole hierarchy exists.
    /// The harness invokes lifecycle explicitly; this is the shared helper.
    public static class Lifecycle
    {
        public static void Invoke(object target, string method)
        {
            if (target == null) return;
            var t = target.GetType();
            while (t != null && t != typeof(object))
            {
                var m = t.GetMethod(method,
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly);
                if (m != null && m.GetParameters().Length == 0)
                {
                    m.Invoke(target, null);
                    return;
                }
                t = t.BaseType;
            }
        }

        public static void Dispatch(object target, string method)
        {
            Invoke(target, method);
        }
    }

    // ------------------------------------------------------------ Transform

    public class Transform : Component
    {
        private Vector3 _position;
        private Quaternion _rotation = Quaternion.identity;
        private Vector3 _localScale = Vector3.one;

        public Transform parent;

        public Vector3 position
        {
            get { return _position; }
            set { _position = value; }
        }

        public Quaternion rotation
        {
            get { return _rotation; }
            set { _rotation = value.normalized; }
        }

        // Headless rig: transforms are flat, so local space is world space. The
        // port only uses these for cosmetic bone offsets, never for physics.
        public Vector3 localPosition
        {
            get { return _position; }
            set { _position = value; }
        }

        public Quaternion localRotation
        {
            get { return _rotation; }
            set { _rotation = value.normalized; }
        }

        public Vector3 localScale
        {
            get { return _localScale; }
            set { _localScale = value; }
        }

        public Vector3 lossyScale { get { return _localScale; } }

        public Vector3 eulerAngles
        {
            get { return _rotation.eulerAngles; }
            set { _rotation = Quaternion.Euler(value); }
        }

        public Vector3 localEulerAngles
        {
            get { return _rotation.eulerAngles; }
            set { _rotation = Quaternion.Euler(value); }
        }

        public Vector3 forward { get { return _rotation * Vector3.forward; } }
        public Vector3 right { get { return _rotation * Vector3.right; } }
        public Vector3 up { get { return _rotation * Vector3.up; } }

        public int childCount { get { return Children.Count; } }

        public List<Transform> Children = new List<Transform>();

        public Transform GetChild(int i) { return Children[i]; }

        public void SetParent(Transform p) { SetParent(p, true); }

        public void SetParent(Transform p, bool worldPositionStays)
        {
            if (parent != null) parent.Children.Remove(this);
            parent = p;
            if (p != null && !p.Children.Contains(this)) p.Children.Add(this);
        }

        public Vector3 TransformPoint(Vector3 p) { return position + rotation * p; }
        public Vector3 TransformDirection(Vector3 d) { return rotation * d; }
        public Vector3 InverseTransformPoint(Vector3 p) { return Quaternion.Inverse(rotation) * (p - position); }
        public Vector3 InverseTransformDirection(Vector3 d) { return Quaternion.Inverse(rotation) * d; }

        public Transform Find(string n)
        {
            if (name == n) return this;
            foreach (var c in Children)
            {
                var r = c.Find(n);
                if (r != null) return r;
            }
            return null;
        }

        public override string ToString() { return name + " " + _position; }
    }

    // ------------------------------------------------------------ GameObject

    public class GameObject : Object
    {
        public readonly List<Component> Components = new List<Component>();
        public Transform transform;
        public string tag = "Untagged";
        public int layer;
        private bool _active = true;

        public GameObject() : this("GameObject") { }

        public GameObject(string goName)
        {
            name = goName;
            transform = new Transform { name = goName, owner = this };
            Register(this);
            Components.Add(transform);
        }

        public GameObject(string goName, params Type[] components) : this(goName)
        {
            foreach (var t in components) AddComponent(t);
        }

        public bool activeSelf { get { return _active; } }
        public bool activeInHierarchy { get { return _active; } }

        public void SetActive(bool value) { _active = value; }

        public T AddComponent<T>() where T : Component, new()
        {
            var c = new T();
            c.owner = this;
            c.name = name;
            Components.Add(c);
            return c;
        }

        public Component AddComponent(Type t)
        {
            var c = (Component)Activator.CreateInstance(t);
            c.owner = this;
            c.name = name;
            Components.Add(c);
            return c;
        }

        public T GetComponent<T>()
        {
            foreach (var c in Components) if (c is T && !c.destroyed) return (T)(object)c;
            return default(T);
        }

        public Component GetComponent(Type t)
        {
            foreach (var c in Components) if (t.IsInstanceOfType(c) && !c.destroyed) return c;
            return null;
        }

        public T GetComponentInParent<T>()
        {
            for (var t = transform; t != null; t = t.parent)
            {
                var go = t.gameObject;
                if (go == null) continue;
                var c = go.GetComponent<T>();
                if (c != null) return c;
            }
            return default(T);
        }

        public T GetComponentInChildren<T>()
        {
            foreach (var c in Components) if (c is T && !c.destroyed) return (T)(object)c;
            foreach (var child in transform.Children)
            {
                var go = child.gameObject;
                if (go == null) continue;
                var c = go.GetComponent<T>();
                if (c != null) return c;
            }
            return default(T);
        }

        public T[] GetComponentsInChildren<T>()
        {
            var list = new List<T>();
            foreach (var c in Components) if (c is T && !c.destroyed) list.Add((T)(object)c);
            foreach (var child in transform.Children)
            {
                var go = child.gameObject;
                if (go == null) continue;
                list.AddRange(go.GetComponentsInChildren<T>());
            }
            return list.ToArray();
        }

        public static GameObject Find(string n)
        {
            foreach (var go in All) if (go.name == n && go.activeInHierarchy) return go;
            return null;
        }

        public static GameObject FindWithTag(string t)
        {
            foreach (var go in All) if (go.tag == t && go.activeInHierarchy) return go;
            return null;
        }

        public static GameObject[] FindGameObjectsWithTag(string t)
        {
            var list = new List<GameObject>();
            foreach (var go in All) if (go.tag == t) list.Add(go);
            return list.ToArray();
        }

        internal static readonly List<GameObject> All = new List<GameObject>();

        internal static void Register(GameObject go) { All.Add(go); }

        internal static void ClearScene() { All.Clear(); }
    }

    // ------------------------------------------------------------ Colliders

    public class Collider : Component
    {
        public bool isTrigger;
        public bool enabled = true;

        public Vector3 ClosestPoint(Vector3 position) { return position; }
    }

    public class BoxCollider : Collider
    {
        public Vector3 center;
        public Vector3 size = Vector3.one;
    }

    public class SphereCollider : Collider
    {
        public Vector3 center;
        public float radius = 0.5f;
    }

    public class CapsuleCollider : Collider
    {
        public Vector3 center;
        public float radius = 0.5f;
        public float height = 2f;
        public int direction = 1;

        public void GetCapsulePoints(out Vector3 a, out Vector3 b)
        {
            var world = transform.TransformPoint(center);
            var h = Mathf.Max(height, radius * 2f) * 0.5f - radius;
            if (direction == 0) { a = world + Vector3.right * h; b = world - Vector3.right * h; }
            else if (direction == 2) { a = world + Vector3.forward * h; b = world - Vector3.forward * h; }
            else { a = world + Vector3.up * h; b = world - Vector3.up * h; }
        }
    }

    public class Rigidbody : Component
    {
        public bool isKinematic;
        public bool useGravity = true;
        public RigidbodyInterpolation interpolation;
        public RigidbodyConstraints constraints;
        public float mass = 1f;
        public float drag;
        public float angularDrag = 0.05f;
        public Vector3 velocity;
        public Vector3 angularVelocity;

        public void MovePosition(Vector3 p)
        {
            // PlayerMotor assigns transform.position right after; honour it here so
            // a body-driven query later still sees the intended pose.
            velocity = (p - transform.position) * PhysicsEngine.InverseFixedDelta;
        }

        public void AddForce(Vector3 f) { velocity += f; }
        public void AddForce(Vector3 f, ForceMode m) { AddForce(f); }
        public void MoveRotation(Quaternion r) { transform.rotation = r; }
    }

    public enum ForceMode { Force = 0, Acceleration = 5, Impulse = 1, VelocityChange = 2 }

    public class CharacterController : Collider
    {
        public float radius = 0.3f;
        public float height = 1.8f;
        public float center = 0.9f;
        public float slopeLimit = 45f;
        public float stepOffset = 0.3f;
        public bool isGrounded { get; set; }

        public CollisionFlags Move(Vector3 motion)
        {
            var flags = PhysicsEngine.MoveCharacter(this, transform.position, motion);
            transform.position = PhysicsEngine.LastCharacterPosition;
            return flags;
        }
    }

    public class RaycastHit
    {
        public Vector3 point;
        public Vector3 normal;
        public float distance;
        public Collider collider;
        public Transform transform { get { return collider != null ? collider.transform : null; } }
    }

    // ------------------------------------------------------------ Render / misc components

    public class Texture : Object { public int width = 4; public int height = 4; }
    public class Texture2D : Texture
    {
        public Texture2D() { }
        public Texture2D(int w, int h) { width = w; height = h; }
    }

    public class CubeTexture : Texture { }

    public class AudioClip : Object { public float length = 0.4f; }
    public class Mesh : Object { }
    public class Material : Object
    {
        public Material() { }
        public Material(Shader s) { shader = s; }
        public Shader shader { get; set; }
        public Texture mainTexture { get; set; }
        public Color color { get; set; }
        public float mainTextureScale { get; set; }
        public bool HasProperty(string n) { return shader != null; }
        public void SetVectorArray(string n, Vector4[] v) { }
        public void SetVector(string n, Vector4 v) { }
        public void SetFloat(string n, float v) { }
        public void SetTexture(string n, Texture t) { }
        public void EnableKeyword(string k) { }
        public void DisableKeyword(string k) { }
    }

    public class Shader : Object
    {
        public string shaderName = "Standard";
        public static Shader Find(string n) { return new Shader { shaderName = n }; }
    }

    public class Renderer : Component
    {
        public bool enabled = true;
        public Material sharedMaterial { get; set; }
        public Material[] sharedMaterials { get; set; }
        public bool castShadows;
    }

    public class MeshRenderer : Renderer { }

    public class ParticleSystem : Component
    {
        public int Emitted;
        public void Emit(int n) { Emitted += n; }
        public void Play() { }
        public void Stop() { }
    }

    public class AudioSource : Component
    {
        public float volume = 1f;
        public int OneShots;
        public void Play() { }
        public void PlayOneShot(AudioClip clip) { PlayOneShot(clip, 1f); }
        public void PlayOneShot(AudioClip clip, float v) { OneShots++; }
    }

    public class Light : Component { public float intensity = 1f; public Color color = Color.white; }

    public class Camera : Behaviour
    {
        public float fieldOfView = 60f;
        public float nearClipPlane = 0.3f;
        public float farClipPlane = 1000f;
        public bool orthographic;
        public Color backgroundColor = Color.black;
        public static Camera main { get; set; }
    }

    public class Animator : Behaviour
    {
        public readonly Dictionary<int, float> Floats = new Dictionary<int, float>();
        public readonly Dictionary<int, bool> Bools = new Dictionary<int, bool>();
        public int StringToHash(string n) { return n.GetHashCode(); }

        public Transform GetBoneTransform(HumanBodyBones bone) { return Bones.ContainsKey(bone) ? Bones[bone] : null; }

        public readonly Dictionary<HumanBodyBones, Transform> Bones = new Dictionary<HumanBodyBones, Transform>();

        public void SetFloat(string n, float v) { SetFloat(n, v, 0.0001f, 0f); }
        public void SetFloat(string n, float v, float damp, float dt) { Floats[StringToHash(n)] = v; }
        public void SetFloat(int h, float v) { Floats[h] = v; }
        public void SetFloat(int h, float v, float damp, float dt) { Floats[h] = v; }
        public void SetBool(string n, bool v) { Bools[StringToHash(n)] = v; }
        public void SetBool(int h, bool v) { Bools[h] = v; }
        public void SetTrigger(string n) { }
    }

    public class TerrainData : Object
    {
        public Vector3 size = new Vector3(1000f, 600f, 1000f);
        public int alphamapWidth = 128;
        public int alphamapHeight = 128;
        public int alphamapLayers = 4;
        public float[,,] Alphamaps;

        public float[,,] GetAlphamaps(int x, int y, int width, int height)
        {
            if (Alphamaps == null) return null;
            var result = new float[height, width, alphamapLayers];
            for (var j = 0; j < height; j++)
                for (var i = 0; i < width; i++)
                    for (var c = 0; c < alphamapLayers; c++)
                        result[j, i, c] = Alphamaps[Math.Min(y + j, alphamapHeight - 1), Math.Min(x + i, alphamapWidth - 1), c];
            return result;
        }
    }

    public class Terrain : Component
    {
        public TerrainData terrainData;
        public static Terrain activeTerrain { get; set; }

        public Vector3 GetPosition() { return transform.position; }
        public Vector3 size { get { return terrainData != null ? terrainData.size : Vector3.one; } }
    }
}