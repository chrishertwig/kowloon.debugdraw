using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;

namespace Kowloon.DebugDraw
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal unsafe struct DrawCmd
    {
        public DrawType Type;
        public RenderMode RenderMode;
        public float Lifetime;
        public Mask Mask;

        // Flexible array member as placeholder for variable size data
        public fixed byte GpuData[1];
    }

    /// <summary> Needs to match DebugLineDraw.hlsl </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct GpuLine
    {
        public float3 Start;
        public uint Color;
        public float3 End;
        public float DepthBias;
    }

    /// <summary> Needs to match DebugLineDraw.hlsl </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct GpuRay
    {
        public float3 Origin;
        public uint Color;
        public float3 Direction;
        public float DepthBias;
    }

    /// <summary> Needs to match DebugLineDraw.hlsl / DebugPolyDraw.hlsl </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct GpuNonUniformScale
    {
        public float3 Center;
        public uint Color;
        public float3 Scale;
        public float DepthBias;
        public float4 Rotation;
    }

    /// <summary> Needs to match DebugLineDraw.hlsl </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct GpuArrow
    {
        public float3 Start;
        public uint Color;
        public float4 Rotation;
        public float Length;
        public float Width;
        public float DepthBias;
        public float Padding;
    }

    /// <summary> Needs to match DebugLineDraw.hlsl </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct GpuCapsule
    {
        public float3 Center;
        public float Radius;
        public float4 Rotation;
        public uint Color;
        public float Height;
        public float DepthBias;
        public float Padding;
    }

    /// <summary> Needs to match DebugLineDraw.hlsl </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct GpuCone
    {
        public float3 Center;
        public float Angle;
        public float4 Rotation;
        public uint Color;
        public float Height;
        public float DepthBias;
        public float Padding;
    }

    /// <summary> Needs to match DebugPolyDraw.hlsl </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct GpuPolyTriangle
    {
        public float3 Vertex0;
        public uint Color;
        public float3 Vertex1;
        public float DepthBias;
        public float3 Vertex2;
        public float Padding2;
    }

    /// <summary> Needs to match DebugPolyDraw.hlsl </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct GpuPolyLine
    {
        public float3 Start;
        public uint Color;
        public float3 End;
        public float Width;
        public float3 Normal;
        public float DepthBias;
    }

    /// <summary> Needs to match DebugText.hlsl </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct GpuText128
    {
        public float3 Position;
        public uint Color;
        public float4 Rotation;
        public float Size;
        public float DepthBias;
        public FixedString128Bytes Chars;
    }
}
