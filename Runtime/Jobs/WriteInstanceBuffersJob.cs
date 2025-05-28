#define ENABLE_DEBUGDRAW_RANGE_CHECKS
using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
#if ENABLE_DEBUGDRAW_RANGE_CHECKS
using Unity.Assertions;
#endif

namespace Kowloon.DebugDraw
{
    /// <summary>
    ///     WriteInstanceBuffersJob copies the GPU data from the draw command buffer to the dedicated graphics buffers
    ///     for each draw type and render mode. It gets executed once per thread.
    /// </summary>
    [BurstCompile]
    internal struct WriteInstanceBuffersJob : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<DrawTypeBufferPointers> GraphicsBufferPointers;

        [ReadOnly]
        public NativeArray<DrawTypeBufferLengths> GraphicsBufferLengths;

        [ReadOnly]
        public DebugDrawCommandBuffer DebugDrawCommandBuffer;

        private DebugDrawCommandBuffer.Enumerator _Enumerator;

        public unsafe void Execute(int chainIndex)
        {
            int4 lineCounter = 0;
            int4 rayCounter = 0;
            int4 squareCounter = 0;
            int4 circleCounter = 0;
            int4 arrowCounter = 0;
            int4 cubeCounter = 0;
            int4 sphereCounter = 0;
            int4 capsuleCounter = 0;
            int4 coneCounter = 0;
            int4 polyTriangleCounter = 0;
            int4 polyLineCounter = 0;
            int4 polyDiscCounter = 0;
            int4 polyCubeCounter = 0;
            int4 polyPlaneCounter = 0;
            int4 text128Counter = 0;

            _Enumerator = DebugDrawCommandBuffer.GetEnumerator(chainIndex);
            while (!_Enumerator.IsDone())
            {
                DrawCmd* drawCmd = _Enumerator.PeekPtr<DrawCmd>();
                switch (drawCmd->Type)
                {
                case DrawType.Line:
                    CopyGpuData<GpuLine>(drawCmd, chainIndex, ref lineCounter);
                    break;
                case DrawType.Ray:
                    CopyGpuData<GpuRay>(drawCmd, chainIndex, ref rayCounter);
                    break;
                case DrawType.Square:
                    CopyGpuData<GpuNonUniformScale>(drawCmd, chainIndex, ref squareCounter);
                    break;
                case DrawType.Circle:
                    CopyGpuData<GpuNonUniformScale>(drawCmd, chainIndex, ref circleCounter);
                    break;
                case DrawType.Arrow:
                    CopyGpuData<GpuArrow>(drawCmd, chainIndex, ref arrowCounter);
                    break;
                case DrawType.Cube:
                    CopyGpuData<GpuNonUniformScale>(drawCmd, chainIndex, ref cubeCounter);
                    break;
                case DrawType.Sphere:
                    CopyGpuData<GpuNonUniformScale>(drawCmd, chainIndex, ref sphereCounter);
                    break;
                case DrawType.Capsule:
                    CopyGpuData<GpuCapsule>(drawCmd, chainIndex, ref capsuleCounter);
                    break;
                case DrawType.Cone:
                    CopyGpuData<GpuCone>(drawCmd, chainIndex, ref coneCounter);
                    break;
                case DrawType.PolyTriangle:
                    CopyGpuData<GpuPolyTriangle>(drawCmd, chainIndex, ref polyTriangleCounter);
                    break;
                case DrawType.PolyLine:
                    CopyGpuData<GpuPolyLine>(drawCmd, chainIndex, ref polyLineCounter);
                    break;
                case DrawType.PolyDisc:
                    CopyGpuData<GpuNonUniformScale>(drawCmd, chainIndex, ref polyDiscCounter);
                    break;
                case DrawType.PolyCube:
                    CopyGpuData<GpuNonUniformScale>(drawCmd, chainIndex, ref polyCubeCounter);
                    break;
                case DrawType.PolyPlane:
                    CopyGpuData<GpuNonUniformScale>(drawCmd, chainIndex, ref polyPlaneCounter);
                    break;
                case DrawType.Text128:
                    CopyGpuData<GpuText128>(drawCmd, chainIndex, ref text128Counter);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
                }
            }
        }

        /// <summary> Copy the GPU data from the draw command to the graphics buffer </summary>
        /// <remarks>
        ///     Each thread has their own dedicated write area in the graphics buffer. We need to calculate the destination
        ///     write position inside the graphics buffer based on the thread index and the current write offset for the thread.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe void CopyGpuData<T>(DrawCmd* drawCmd, int chainIndex, ref int4 counter) where T : unmanaged
        {
            void* graphicsBufferStartPtr = drawCmd->RenderMode switch
            {
                RenderMode.DepthTest => GraphicsBufferPointers[(int)drawCmd->Type].DepthTest,
                RenderMode.Always => GraphicsBufferPointers[(int)drawCmd->Type].Always,
                RenderMode.SeeThrough => GraphicsBufferPointers[(int)drawCmd->Type].SeeThrough,
                RenderMode.Transparent => GraphicsBufferPointers[(int)drawCmd->Type].Transparent,
                _ => throw new ArgumentOutOfRangeException()
            };

            int elementWriteCounter = counter[(int)drawCmd->RenderMode];
            int threadOffset = DebugDrawCommandBuffer.GetPerThreadOffset(chainIndex, drawCmd->Type, drawCmd->RenderMode);
            T* destinationPtr = (T*)graphicsBufferStartPtr + threadOffset + elementWriteCounter;

#if ENABLE_DEBUGDRAW_RANGE_CHECKS
            int graphicsBufferLength = drawCmd->RenderMode switch
            {
                RenderMode.DepthTest => GraphicsBufferLengths[(int)drawCmd->Type].DepthTest,
                RenderMode.Always => GraphicsBufferLengths[(int)drawCmd->Type].Always,
                RenderMode.SeeThrough => GraphicsBufferLengths[(int)drawCmd->Type].SeeThrough,
                RenderMode.Transparent => GraphicsBufferLengths[(int)drawCmd->Type].Transparent,
                _ => throw new ArgumentOutOfRangeException()
            };
            void* graphicsBufferEndPtr = (byte*)graphicsBufferStartPtr + graphicsBufferLength;
            Assert.IsTrue(destinationPtr >= graphicsBufferStartPtr, $"Buffer underflow. destinationPtr={(long)destinationPtr:X}, graphicsBufferStartPtr={(long)graphicsBufferStartPtr:X}");
            Assert.IsTrue((byte*)destinationPtr + sizeof(T) <= graphicsBufferEndPtr, $"Buffer overflow. destinationPtr={(long)destinationPtr:X}, graphicsBufferEndPtr={(long)graphicsBufferEndPtr:X}");
#endif
            // Copy GPU data to graphics buffer
            UnsafeUtility.MemCpy(destinationPtr, drawCmd->GpuData, sizeof(T));

            // Directly increment the counter value to avoid bounds check in int4 array implementation. Same as counter[(int)drawCmd->RenderMode]++
            fixed (int* ptr = &counter.x) ptr[(int)drawCmd->RenderMode]++;

            // Move the pointer to the next draw command
            // The trailing byte accounts for the flexible array member at the end of DrawCmd which is just a placeholder for variable size data (GpuData).
            // We need to subtract the placeholder byte to get the correct size of the whole draw command data.
            const int trailingByte = 1;
            _Enumerator.Next(sizeof(DrawCmd) - trailingByte + sizeof(T));
        }
    }
}