using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Kowloon.DebugDraw.Tests
{
    public class DebugDrawCommandBufferTests : ECSTestsFixture
    {
        private bool IsEqual(double a, double b)
        {
            return math.abs(a - b) < 1e-10;
        }

        private bool IsEqual(float a, float b)
        {
            return math.abs(a - b) < 1e-5f;
        }

        private void ScheduleJobOnWorkerThreadAndWaitForComplete<T>(T job) where T : struct, IJob
        {
            JobHandle jobHandle = job.Schedule();

            // Pretend the main thread is busy so the scheduled job executes on an actual worker thread.
            while (!jobHandle.IsCompleted)
            {
                System.Threading.Thread.Sleep(1);
            }

            // We should be completed by now, but we still need to call Complete for the safety system to not complain.
            jobHandle.Complete();
        }

        [Test]
        public unsafe void CreateWriteDispose_MainThread()
        {
            DebugDrawCommandBuffer buffer = new(Allocator.Persistent);
            Assert.IsTrue(buffer.IsCreated);

            buffer.Draw.Sphere(float3.zero, quaternion.identity, 1f, Color.red);

            DebugDrawCommandBuffer.Enumerator enumerator = buffer.GetEnumerator(0);

            Assert.IsFalse(enumerator.IsDone());

            DrawCmd* drawCmd = enumerator.PeekPtr<DrawCmd>();
            Assert.IsTrue(drawCmd->Type == DrawType.Sphere);
            Assert.IsTrue(drawCmd->Mask == Mask.KeepOneFrame);
            Assert.IsTrue(drawCmd->Lifetime == 0f);

            GpuNonUniformScale* gpuNonUniformScale = (GpuNonUniformScale*) drawCmd->GpuData;
            Assert.IsTrue(gpuNonUniformScale->Center.Equals(float3.zero));
            Assert.IsTrue(gpuNonUniformScale->Scale.Equals(new float3(1f, 1f, 1f)));
            Assert.IsTrue(gpuNonUniformScale->Rotation.Equals(quaternion.identity.value));
            Assert.IsTrue(IsEqual(gpuNonUniformScale->DepthBias, 0f));

            int size = sizeof(DrawCmd) - 1 + sizeof(GpuNonUniformScale);
            enumerator.Next(size);
            Assert.IsTrue(enumerator.IsDone());

            buffer.Dispose();
            Assert.IsFalse(buffer.IsCreated);
        }

        [Test]
        public void ClearCounters_MainThread()
        {
            DebugDrawCommandBuffer buffer = new(Allocator.Persistent);
            Assert.IsTrue(buffer.IsCreated);

            buffer.Draw.Sphere(float3.zero, quaternion.identity, 1f, Color.red);
            buffer.Draw.Sphere(float3.zero, quaternion.identity, 1f, Color.red);
            buffer.Draw.Sphere(float3.zero, quaternion.identity, 1f, Color.red);

            Assert.AreEqual(3, buffer.GetTotalDrawCount(DrawType.Sphere, RenderMode.DepthTest));

            buffer.ClearDrawCountersForChain(0);

            Assert.Zero(buffer.GetTotalDrawCount(DrawType.Sphere, RenderMode.DepthTest));

            buffer.Dispose();
            Assert.IsFalse(buffer.IsCreated);
        }

        private struct DrawSpheresFromWorkerThreadJob : IJob
        {
            public DebugDrawCommandBuffer.ParallelWriter Draw;

            public NativeArray<int> ThreadIndex;

            public void Execute()
            {
                ThreadIndex[0] = JobsUtility.ThreadIndex;
                Draw.Draw.Sphere(float3.zero, quaternion.identity, 1f, Color.red, 0f);
                Draw.Draw.Sphere(float3.zero, quaternion.identity, 1f, Color.red, 2f);
                Draw.Draw.Sphere(float3.zero, quaternion.identity, 1f, Color.red, 10f);
            }
        }

        [Test]
        public void ClearCounters_WorkerThread()
        {
            DebugDrawCommandBuffer buffer = new(Allocator.Persistent);
            Assert.IsTrue(buffer.IsCreated);

            buffer.Draw.Sphere(float3.zero, quaternion.identity, 1f, Color.red);
            buffer.Draw.Sphere(float3.zero, quaternion.identity, 1f, Color.red);
            buffer.Draw.Sphere(float3.zero, quaternion.identity, 1f, Color.red);

            NativeArray<int> workerThreadIndex = new(1, Allocator.Persistent);
            ScheduleJobOnWorkerThreadAndWaitForComplete(new DrawSpheresFromWorkerThreadJob
            {
                Draw = buffer.AsParallelWriter(),
                ThreadIndex = workerThreadIndex
            });

            Assert.AreEqual(6, buffer.GetTotalDrawCount(DrawType.Sphere, RenderMode.DepthTest));

            buffer.ClearDrawCountersForChain(workerThreadIndex[0]);
            Assert.AreEqual(3, buffer.GetTotalDrawCount(DrawType.Sphere, RenderMode.DepthTest));

            buffer.ClearDrawCountersForChain(0);
            Assert.Zero(buffer.GetTotalDrawCount(DrawType.Sphere, RenderMode.DepthTest));

            buffer.Dispose();
            Assert.IsFalse(buffer.IsCreated);
        }

        [Test]
        public unsafe void Defrag()
        {
            DebugDrawCommandBuffer buffer = new(Allocator.Persistent);

            buffer.Draw.Sphere(float3.zero, quaternion.identity, 1f, Color.red);
            buffer.Draw.Sphere(float3.zero, quaternion.identity, 1f, Color.red);
            buffer.Draw.Sphere(float3.zero, quaternion.identity, 1f, Color.red);

            NativeArray<int> workerThreadIndex = new(1, Allocator.Persistent);
            ScheduleJobOnWorkerThreadAndWaitForComplete(new DrawSpheresFromWorkerThreadJob
            {
                Draw = buffer.AsParallelWriter(),
                ThreadIndex = workerThreadIndex
            });

            int threadIndex = workerThreadIndex[0];
            const float deltaTime = 5f;

            // Get enumerator before resetting anything so we can loop over old chunks.
            DebugDrawCommandBuffer.Enumerator enumerator = buffer.GetEnumerator(threadIndex);
            Mask clearMask = buffer.ClearMask;

            Assert.IsFalse(enumerator.IsDone());

            // Set a fresh start chunk and clear draw counters for this thread / chunk chain.
            buffer.SetNewStartChunk(threadIndex, out DataChunk* oldStartChunk);
            buffer.ClearDrawCountersForChain(threadIndex);

            // Copy over all persisting draw commands to new start chunk.
            while (!enumerator.IsDone())
            {
                DrawCmd* drawCmd = enumerator.PeekPtr<DrawCmd>();
                int size = DefragRemainingDrawsJob.GetCmdSize(drawCmd->Type);

                Mask mask = drawCmd->Mask;
                float lifetime = drawCmd->Lifetime - deltaTime;

                // Check if this draw command has any bits set other than KeepOneFrame
                bool hasValidMaskSet = (mask & ~Mask.KeepOneFrame) != 0;

                // We have at least one matching bit set in the mask
                bool cullDrawCmd = (mask & clearMask) != 0;

                // Check if we should keep this draw command for one more frame
                bool keepOneFrame = (mask & Mask.KeepOneFrame) != 0;

                // Keep if lifetime remains and no mask is set. Or if no cull mask is set. Or if keep one frame flag is set.
                bool shouldKeepDrawCmd = (lifetime > 0 && !hasValidMaskSet) || (!cullDrawCmd && hasValidMaskSet) || (keepOneFrame && hasValidMaskSet);
                if (shouldKeepDrawCmd)
                {
                    drawCmd->Lifetime = lifetime;
                    drawCmd->Mask &= ~Mask.KeepOneFrame; // Make sure keep one frame flag is off

                    void* destination = buffer.Add(threadIndex, size);
                    UnsafeUtility.MemCpy(destination, drawCmd, size);
                    buffer.IncrementDrawCounter(threadIndex, drawCmd->Type, RenderMode.DepthTest);
                }

                enumerator.Next(size);
            }

            Assert.AreEqual(4, buffer.GetTotalDrawCount(DrawType.Sphere, RenderMode.DepthTest));

            // Free old chunk chain.
            DataChunk* chunk = oldStartChunk;
            while (chunk != null)
            {
                DataChunk* nextChunk = chunk->Next;
                buffer.FreeChunk(chunk);
                chunk = nextChunk;
            }

            enumerator = buffer.GetEnumerator(threadIndex);
            Assert.IsFalse(enumerator.IsDone());

            int count = 0;
            while (!enumerator.IsDone())
            {
                DrawCmd* drawCmd = enumerator.PeekPtr<DrawCmd>();
                int size = DefragRemainingDrawsJob.GetCmdSize(drawCmd->Type);
                Assert.IsTrue(drawCmd->Type == DrawType.Sphere);
                count++;
                enumerator.Next(size);
            }
            Assert.AreEqual(1, count);

            buffer.Dispose();
            Assert.IsFalse(buffer.IsCreated);
        }
    }
}