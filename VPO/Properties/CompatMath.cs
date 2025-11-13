using UnityEngine;
using float2 = UnityEngine.Vector2;
using float3 = UnityEngine.Vector3;
using float4 = UnityEngine.Vector4;

namespace VPO.Compat
{
    public static class math
    {
        // scalar
        public static float abs(float x) => Mathf.Abs(x);
        public static float min(float a, float b) => Mathf.Min(a, b);
        public static float max(float a, float b) => Mathf.Max(a, b);
        public static float clamp(float v, float a, float b) => Mathf.Clamp(v, a, b);
        public static float saturate(float v) => Mathf.Clamp01(v);
        public static float lerp(float a, float b, float t) => Mathf.Lerp(a, b, t);

        // ВАЖНО: перегрузки для int, чтобы не ловить "float → int"
        public static int min(int a, int b) => a < b ? a : b;
        public static int max(int a, int b) => a > b ? a : b;

        // vectors
        public static float length(float3 v) => v.magnitude;
        public static float length(float2 v) => v.magnitude;
        public static float lengthsq(float3 v) => v.sqrMagnitude;     // ← вот её не хватало
        public static float lengthsq(float2 v) => v.sqrMagnitude;
        public static float dot(float3 a, float3 b) => Vector3.Dot(a, b);
        public static float3 cross(float3 a, float3 b) => Vector3.Cross(a, b);
        public static float3 normalize(float3 v) => v.sqrMagnitude > 0f ? v.normalized : Vector3.zero;
        public static float distance(float3 a, float3 b) => Vector3.Distance(a, b);
        public static float3 lerp(float3 a, float3 b, float t) => Vector3.Lerp(a, b, t);
    }
}
