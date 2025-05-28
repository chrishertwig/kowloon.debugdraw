using Unity.Collections;
using Unity.Mathematics;
using Color = UnityEngine.Color;

namespace Kowloon.DebugDraw
{
    public static class Draw
    {
        private static DebugDrawCommands DrawCommands => DebugDrawCommandBuffer.Instance.Data.Draw;

        public static void AddToClearMask(Mask mask)
            => DebugDrawCommandBuffer.Instance.Data.AddToClearMask(mask);

        public static void Line(float3 start, float3 end, Color color, float lifetime = 0f, RenderMode renderMode = RenderMode.DepthTest, float depthBias = 0f, Mask mask = Mask.None)
            => DrawCommands.Line(start, end, color, lifetime, renderMode, depthBias, mask);

        public static void Ray(float3 start, float3 direction, Color color, float lifetime = 0f, RenderMode renderMode = RenderMode.DepthTest, float depthBias = 0f, Mask mask = Mask.None)
            => DrawCommands.Ray(start, direction, color, lifetime, renderMode, depthBias, mask);

        public static void Square(float3 center, quaternion rotation, float2 size, Color color, float lifetime = 0f, RenderMode renderMode = RenderMode.DepthTest, float depthBias = 0f, Mask mask = Mask.None)
            => DrawCommands.Square(center, rotation, size, color, lifetime, renderMode, depthBias, mask);

        public static void Circle(float3 center, quaternion rotation, float radius, Color color, float lifetime = 0f, RenderMode renderMode = RenderMode.DepthTest, float depthBias = 0f, Mask mask = Mask.None)
            => DrawCommands.Circle(center, rotation, radius, color, lifetime, renderMode, depthBias, mask);

        public static void Arrow(float3 center, quaternion rotation, float length, float width, Color color, float lifetime = 0f, RenderMode renderMode = RenderMode.DepthTest, float depthBias = 0f, Mask mask = Mask.None)
            => DrawCommands.Arrow(center, rotation, length, width, color, lifetime, renderMode, depthBias, mask);

        public static void Cube(float3 center, quaternion rotation, float3 size, Color color, float lifetime = 0f, RenderMode renderMode = RenderMode.DepthTest, float depthBias = 0f, Mask mask = Mask.None)
            => DrawCommands.Cube(center, rotation, size, color, lifetime, renderMode, depthBias, mask);

        public static void Sphere(float3 center, quaternion rotation, float radius, Color color, float lifetime = 0f, RenderMode renderMode = RenderMode.DepthTest, float depthBias = 0f, Mask mask = Mask.None)
            => DrawCommands.Sphere(center, rotation, radius, color, lifetime, renderMode, depthBias, mask);

        public static void Capsule(float3 center, quaternion rotation, float radius, float height, Color color, float lifetime = 0f, RenderMode renderMode = RenderMode.DepthTest, float depthBias = 0f, Mask mask = Mask.None)
            => DrawCommands.Capsule(center, rotation, radius, height, color, lifetime, renderMode, depthBias, mask);

        public static void Cone(float3 center, quaternion rotation, float angle, float height, Color color, float lifetime = 0f, RenderMode renderMode = RenderMode.DepthTest, float depthBias = 0f, Mask mask = Mask.None)
            => DrawCommands.Cone(center, rotation, angle, height, color, lifetime, renderMode, depthBias, mask);

        public static void PolyTriangle(float3 a, float3 b, float3 c, Color color, float lifetime = 0f, RenderMode renderMode = RenderMode.DepthTest, float depthBias = 0f, Mask mask = Mask.None)
            => DrawCommands.PolyTriangle(a, b, c, color, lifetime, renderMode, depthBias, mask);

        public static void PolyLine(float3 start, float3 end, float3 normal, float width, Color color, float lifetime = 0f, RenderMode renderMode = RenderMode.DepthTest, float depthBias = 0f, Mask mask = Mask.None)
            => DrawCommands.PolyLine(start, end, normal, width, color, lifetime, renderMode, depthBias, mask);

        public static void PolyDisc(float3 center, float3 normal, float radius, Color color, float lifetime = 0f, RenderMode renderMode = RenderMode.DepthTest, float depthBias = 0f, Mask mask = Mask.None)
            => DrawCommands.PolyDisc(center, normal, radius, color, lifetime, renderMode, depthBias, mask);

        public static void PolyCube(float3 center, quaternion rotation, float3 size, Color color, float lifetime = 0f, RenderMode renderMode = RenderMode.DepthTest, float depthBias = 0f, Mask mask = Mask.None)
            => DrawCommands.PolyCube(center, rotation, size, color, lifetime, renderMode, depthBias, mask);

        public static void PolyPlane(float3 center, quaternion rotation, float2 size, Color color, float lifetime = 0f, RenderMode renderMode = RenderMode.DepthTest, float depthBias = 0f, Mask mask = Mask.None)
            => DrawCommands.PolyPlane(center, rotation, size, color, lifetime, renderMode, depthBias, mask);

        public static void Text(float3 position, quaternion rotation, float size, Color color, FixedString128Bytes text, float lifetime = 0f, RenderMode renderMode = RenderMode.DepthTest, float depthBias = 0f, Mask mask = Mask.None)
            => DrawCommands.Text(position, rotation, size, color, text, lifetime, renderMode, depthBias, mask);
    }
}