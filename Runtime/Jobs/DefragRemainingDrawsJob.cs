using System;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace Kowloon.DebugDraw
{
    [BurstCompile]
    public struct DefragRemainingDrawsJob : IJobParallelFor
    {
        public float DeltaTime;

        public Mask ClearMask;

        public DebugDrawCommandBuffer DebugDrawCommandBuffer;

        public unsafe void Execute(int threadIndex)
        {
            // Get enumerator before resetting anything so we can loop over old chunks.
            DebugDrawCommandBuffer.Enumerator enumerator = DebugDrawCommandBuffer.GetEnumerator(threadIndex);
            ClearMask = DebugDrawCommandBuffer.ClearMask;

            // Nothing to do here. Just return.
            if (enumerator.IsDone()) return;

            // Set a fresh start chunk and clear draw counters for this thread / chunk chain.
            DebugDrawCommandBuffer.SetNewStartChunk(threadIndex, out DataChunk* oldStartChunk);
            DebugDrawCommandBuffer.ClearDrawCountersForChain(threadIndex);

            // Copy over all persisting draw commands to new start chunk.
            while (!enumerator.IsDone())
            {
                DrawCmd* drawCmd = enumerator.PeekPtr<DrawCmd>();
                int size = GetCmdSize(drawCmd->Type);

                Mask mask = drawCmd->Mask;
                float lifetime = drawCmd->Lifetime - DeltaTime;

                // Check if this draw command has any bits set other than KeepOneFrame
                bool hasValidMaskSet = (mask & ~Mask.KeepOneFrame) != 0;

                // We have at least one matching bit set in the mask
                bool cullDrawCmd = (mask & ClearMask) != 0;

                // Check if we should keep this draw command for one more frame
                bool keepOneFrame = (mask & Mask.KeepOneFrame) != 0;

                // Keep if lifetime remains and no mask is set. Or if no cull mask is set. Or if keep one frame flag is set.
                bool shouldKeepDrawCmd = (lifetime > 0 && !hasValidMaskSet) || (!cullDrawCmd && hasValidMaskSet) || (keepOneFrame && hasValidMaskSet);
                if (shouldKeepDrawCmd)
                {
                    drawCmd->Lifetime = lifetime;
                    drawCmd->Mask &= ~Mask.KeepOneFrame; // Make sure keep one frame flag is off

                    void* destination = DebugDrawCommandBuffer.Add(threadIndex, size);
                    UnsafeUtility.MemCpy(destination, drawCmd, size);
                    DebugDrawCommandBuffer.IncrementDrawCounter(threadIndex, drawCmd->Type, drawCmd->RenderMode);
                }

                enumerator.Next(size);
            }

            // Free old chunk chain.
            DataChunk* chunk = oldStartChunk;
            while (chunk != null)
            {
                DataChunk* nextChunk = chunk->Next;
                DebugDrawCommandBuffer.FreeChunk(chunk);
                chunk = nextChunk;
            }
        }

        internal static unsafe int GetCmdSize(DrawType type)
        {
            const int trailingByte = 1;
            return type switch
            {
                DrawType.Line => sizeof(DrawCmd) - trailingByte + sizeof(GpuLine),
                DrawType.Ray => sizeof(DrawCmd) - trailingByte + sizeof(GpuRay),
                DrawType.Square => sizeof(DrawCmd) - trailingByte + sizeof(GpuNonUniformScale),
                DrawType.Circle => sizeof(DrawCmd) - trailingByte + sizeof(GpuNonUniformScale),
                DrawType.Arrow => sizeof(DrawCmd) - trailingByte + sizeof(GpuArrow),
                DrawType.Cube => sizeof(DrawCmd) - trailingByte + sizeof(GpuNonUniformScale),
                DrawType.Sphere => sizeof(DrawCmd) - trailingByte + sizeof(GpuNonUniformScale),
                DrawType.Capsule => sizeof(DrawCmd) - trailingByte + sizeof(GpuCapsule),
                DrawType.Cone => sizeof(DrawCmd) - trailingByte + sizeof(GpuCone),
                DrawType.PolyTriangle => sizeof(DrawCmd) - trailingByte + sizeof(GpuPolyTriangle),
                DrawType.PolyLine => sizeof(DrawCmd) - trailingByte + sizeof(GpuPolyLine),
                DrawType.PolyDisc => sizeof(DrawCmd) - trailingByte + sizeof(GpuNonUniformScale),
                DrawType.PolyCube => sizeof(DrawCmd) - trailingByte + sizeof(GpuNonUniformScale),
                DrawType.PolyPlane => sizeof(DrawCmd) - trailingByte + sizeof(GpuNonUniformScale),
                DrawType.Text128 => sizeof(DrawCmd) - trailingByte + sizeof(GpuText128),
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }
}
