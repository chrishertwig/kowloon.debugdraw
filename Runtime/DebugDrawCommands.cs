using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Kowloon.DebugDraw
{
    [BurstCompile]
    public struct DebugDrawCommands
    {
        [NativeDisableUnsafePtrRestriction]
        private readonly unsafe CommandBufferInternal* _Buffer;

        internal unsafe DebugDrawCommands(CommandBufferInternal* buffer)
        {
            _Buffer = buffer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe void WriteNextDrawCmd<T>(DrawType type, RenderMode renderMode, float lifetime, Mask mask, out T* gpuData) where T : unmanaged
        {
            int threadIndex = JobsUtility.ThreadIndex;

            const int trailingByte = 1;
            int totalCmdSize = sizeof(DrawCmd) - trailingByte + sizeof(T);

            byte* destinationPtr = (byte*)CommandBufferInternal.Add(_Buffer, threadIndex, totalCmdSize);

            DrawCmd* cmd = (DrawCmd*)destinationPtr;
            cmd->Type = type;
            cmd->RenderMode = renderMode;
            cmd->Lifetime = lifetime;
            cmd->Mask = mask | Mask.KeepOneFrame;
            gpuData = (T*)cmd->GpuData;

            _Buffer->IncrementDrawCounter(threadIndex, type, renderMode);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint PackColor(Color color)
        {
            return ((uint)(color.r * 255f) << 24) |
                   ((uint)(color.g * 255f) << 16) |
                   ((uint)(color.b * 255f) << 8)  |
                   (uint)(color.a * 255f);
        }

        [BurstCompile]
        public unsafe void Line(float3 start, float3 end, Color color, float lifetime = 0f, RenderMode renderMode = RenderMode.DepthTest, float depthBias = 0f, Mask mask = Mask.None)
        {
            if (_Buffer->IsReadOnly) return;
            WriteNextDrawCmd(DrawType.Line, renderMode, lifetime, mask, out GpuLine* line);
            line->Start = start;
            line->Color = PackColor(color);
            line->End = end;
            line->DepthBias = depthBias;
        }

        [BurstCompile]
        public unsafe void Ray(float3 start, float3 direction, Color color, float lifetime = 0f, RenderMode renderMode = RenderMode.DepthTest, float depthBias = 0f, Mask mask = Mask.None)
        {
            if (_Buffer->IsReadOnly) return;
            WriteNextDrawCmd(DrawType.Ray, renderMode, lifetime, mask, out GpuRay* ray);
            ray->Origin = start;
            ray->Color = PackColor(color);
            ray->Direction = direction;
            ray->DepthBias = depthBias;
        }

        [BurstCompile]
        public unsafe void Square(float3 center, quaternion rotation, float2 size, Color color, float lifetime = 0f, RenderMode renderMode = RenderMode.DepthTest, float depthBias = 0f, Mask mask = Mask.None)
        {
            if (_Buffer->IsReadOnly) return;
            WriteNextDrawCmd(DrawType.Square, renderMode, lifetime, mask, out GpuNonUniformScale* square);
            square->Center = center;
            square->Color = PackColor(color);
            square->Scale = new float3(size.x, size.y, 1f);
            square->DepthBias = depthBias;
            square->Rotation = rotation.value;
        }

        [BurstCompile]
        public unsafe void Circle(float3 center, quaternion rotation, float radius, Color color, float lifetime = 0f, RenderMode renderMode = RenderMode.DepthTest, float depthBias = 0f, Mask mask = Mask.None)
        {
            if (_Buffer->IsReadOnly) return;
            WriteNextDrawCmd(DrawType.Circle, renderMode, lifetime, mask, out GpuNonUniformScale* circle);
            circle->Center = center;
            circle->Color = PackColor(color);
            circle->Scale = radius;
            circle->DepthBias = depthBias;
            circle->Rotation = rotation.value;
        }

        [BurstCompile]
        public unsafe void Arrow(float3 center, quaternion rotation, float length, float width, Color color, float lifetime = 0f, RenderMode renderMode = RenderMode.DepthTest, float depthBias = 0f, Mask mask = Mask.None)
        {
            if (_Buffer->IsReadOnly) return;
            WriteNextDrawCmd(DrawType.Arrow, renderMode, lifetime, mask, out GpuArrow* arrow);
            arrow->Start = center;
            arrow->Color = PackColor(color);
            arrow->Rotation = rotation.value;
            arrow->Length = length;
            arrow->Width = width;
            arrow->DepthBias = depthBias;
        }

        [BurstCompile]
        public unsafe void Cube(float3 center, quaternion rotation, float3 size, Color color, float lifetime = 0f, RenderMode renderMode = RenderMode.DepthTest, float depthBias = 0f, Mask mask = Mask.None)
        {
            if (_Buffer->IsReadOnly) return;
            WriteNextDrawCmd(DrawType.Cube, renderMode, lifetime, mask, out GpuNonUniformScale* cube);
            cube->Center = center;
            cube->Color = PackColor(color);
            cube->Scale = size;
            cube->DepthBias = depthBias;
            cube->Rotation = rotation.value;
        }

        [BurstCompile]
        public unsafe void Sphere(float3 center, quaternion rotation, float radius, Color color, float lifetime = 0f, RenderMode renderMode = RenderMode.DepthTest, float depthBias = 0f, Mask mask = Mask.None)
        {
            if (_Buffer->IsReadOnly) return;
            WriteNextDrawCmd(DrawType.Sphere, renderMode, lifetime, mask, out GpuNonUniformScale* sphere);
            sphere->Center = center;
            sphere->Color = PackColor(color);
            sphere->Scale = radius;
            sphere->DepthBias = depthBias;
            sphere->Rotation = rotation.value;
        }

        [BurstCompile]
        public unsafe void Capsule(float3 center, quaternion rotation, float radius, float height, Color color, float lifetime = 0f, RenderMode renderMode = RenderMode.DepthTest, float depthBias = 0f, Mask mask = Mask.None)
        {
            if (_Buffer->IsReadOnly) return;
            WriteNextDrawCmd(DrawType.Capsule, renderMode, lifetime, mask, out GpuCapsule* capsule);
            capsule->Center = center;
            capsule->Radius = radius;
            capsule->Rotation = rotation.value;
            capsule->Color = PackColor(color);
            capsule->Height = height;
            capsule->DepthBias = depthBias;
        }

        [BurstCompile]
        public unsafe void Cone(float3 center, quaternion rotation, float angle, float height, Color color, float lifetime = 0f, RenderMode renderMode = RenderMode.DepthTest, float depthBias = 0f, Mask mask = Mask.None)
        {
            if (_Buffer->IsReadOnly) return;
            WriteNextDrawCmd(DrawType.Cone, renderMode, lifetime, mask, out GpuCone* cone);
            cone->Center = center;
            cone->Angle = angle;
            cone->Rotation = rotation.value;
            cone->Color = PackColor(color);
            cone->Height = height;
            cone->DepthBias = depthBias;
        }

        [BurstCompile]
        public unsafe void PolyTriangle(float3 a, float3 b, float3 c, Color color, float lifetime = 0f, RenderMode renderMode = RenderMode.DepthTest, float depthBias = 0f, Mask mask = Mask.None)
        {
            if (_Buffer->IsReadOnly) return;
            WriteNextDrawCmd(DrawType.PolyTriangle, renderMode, lifetime, mask, out GpuPolyTriangle* triangle);
            triangle->DepthBias = depthBias;
            triangle->Vertex0 = a;
            triangle->Vertex1 = b;
            triangle->Vertex2 = c;
            triangle->Color = PackColor(color);
        }

        [BurstCompile]
        public unsafe void PolyLine(float3 start, float3 end, float3 normal, float width, Color color, float lifetime = 0f, RenderMode renderMode = RenderMode.DepthTest, float depthBias = 0f, Mask mask = Mask.None)
        {
            if (_Buffer->IsReadOnly) return;
            WriteNextDrawCmd(DrawType.PolyLine, renderMode, lifetime, mask, out GpuPolyLine* line);
            line->DepthBias = depthBias;
            line->Start = start;
            line->End = end;
            line->Color = PackColor(color);
            line->Normal = normal;
            line->Width = width;
        }

        [BurstCompile]
        public unsafe void PolyDisc(float3 center, float3 normal, float radius, Color color, float lifetime = 0f, RenderMode renderMode = RenderMode.DepthTest, float depthBias = 0f, Mask mask = Mask.None)
        {
            if (_Buffer->IsReadOnly) return;
            WriteNextDrawCmd(DrawType.PolyDisc, renderMode, lifetime, mask, out GpuNonUniformScale* disc);
            bool pointsUp = math.dot(math.normalize(normal), math.up()) > 1.0f - math.EPSILON;
            disc->DepthBias = depthBias;
            disc->Center = center;
            disc->Rotation = quaternion.LookRotation(normal, pointsUp ? math.right() : math.up()).value;
            disc->Scale = radius;
            disc->Color = PackColor(color);
        }

        [BurstCompile]
        public unsafe void PolyCube(float3 center, quaternion rotation, float3 size, Color color, float lifetime = 0f, RenderMode renderMode = RenderMode.DepthTest, float depthBias = 0f, Mask mask = Mask.None)
        {
            if (_Buffer->IsReadOnly) return;
            WriteNextDrawCmd(DrawType.PolyCube, renderMode, lifetime, mask, out GpuNonUniformScale* cube);
            cube->DepthBias = depthBias;
            cube->Center = center;
            cube->Rotation = rotation.value;
            cube->Scale= size;
            cube->Color = PackColor(color);
        }

        [BurstCompile]
        public unsafe void PolyPlane(float3 center, quaternion rotation, float2 size, Color color, float lifetime = 0f, RenderMode renderMode = RenderMode.DepthTest, float depthBias = 0f, Mask mask = Mask.None)
        {
            if (_Buffer->IsReadOnly) return;
            WriteNextDrawCmd(DrawType.PolyPlane, renderMode, lifetime, mask, out GpuNonUniformScale* plane);
            plane->DepthBias = depthBias;
            plane->Center = center;
            plane->Rotation = rotation.value;
            plane->Scale = new float3(size.x, 0f, size.y);
            plane->Color = PackColor(color);
        }

        [BurstCompile]
        public unsafe void Text(float3 position, quaternion rotation, float size, Color color, FixedString128Bytes text, float lifetime = 0f, RenderMode renderMode = RenderMode.DepthTest, float depthBias = 0f, Mask mask = Mask.None)
        {
            if (_Buffer->IsReadOnly) return;
            WriteNextDrawCmd(DrawType.Text128, renderMode, lifetime, mask, out GpuText128* gpuText);
            gpuText->DepthBias = depthBias;
            gpuText->Position = position;
            gpuText->Rotation = rotation.value;
            gpuText->Size = size;
            gpuText->Color = PackColor(color);
            gpuText->Chars = text;
        }
    }
}