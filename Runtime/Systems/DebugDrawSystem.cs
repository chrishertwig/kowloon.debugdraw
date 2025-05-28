using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Kowloon.DebugDraw
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(BeginPresentationEntityCommandBufferSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    public partial class DebugDrawSystem : SystemBase
    {
        private static readonly ProfilerMarker ProfileWaitForReadJob = new("DebugDrawSystem.WaitForReadJob");
        private static readonly ProfilerMarker ProfileReCreateBuffer = new("DebugDrawSystem.ReCreateBuffer");
        private static readonly ProfilerMarker ProfileUnlockBuffer = new("DebugDrawSystem.UnlockBuffer");
        private static readonly ProfilerMarker ProfileEnqueuePass = new("DebugDrawSystem.EnqueuePass");

        private DebugDrawRenderPass _DebugDrawRenderPass;
        private InstanceBufferManager _InstanceBufferManager;

        private JobHandle _WriteInstanceBuffersJobHandle;
        private JobHandle _DefragBufferJobHandle;

        private bool _ReadDataOutstanding;

        // Event we can hook into to add our own debug draw commands before the system processes them.
        // This is mostly of use for non-ecs systems that want to add debug draw commands.
        public static event Action OnBeginUpdate = delegate { };

        protected override void OnCreate()
        {
            RequireForUpdate<DebugDrawSettings>();

            DebugDrawCommandBuffer.Instance.Data = new DebugDrawCommandBuffer(Allocator.Persistent);
        }

        protected override void OnDestroy()
        {
            DebugDrawCommandBuffer.Instance.Data.Dispose();
        }

        protected override void OnStartRunning()
        {
            DebugDrawCommandBuffer.Instance.Data.Reset();

            DebugDrawSettings settings = SystemAPI.GetSingleton<DebugDrawSettings>();

            if (settings.FontTexture.Value == null)
            {
                Enabled = false;
                return;
            }

            ShapeBuilder.GenerateSquare(out NativeArray<float3> squareVertices, out NativeArray<int> squareIndices, Allocator.Temp);
            ShapeBuilder.GenerateCube(out NativeArray<float3> cubeVertices, out NativeArray<int> cubeIndices, Allocator.Temp);
            ShapeBuilder.GenerateCircle(out NativeArray<float3> circleVertices, out NativeArray<int> circleIndices, Allocator.Temp);
            ShapeBuilder.GenerateSphere(out NativeArray<float3> sphereVertices, out NativeArray<int> sphereIndices, Allocator.Temp);
            ShapeBuilder.GenerateCapsule(out NativeArray<float3> capsuleVertices, out NativeArray<int> capsuleIndices, Allocator.Temp);
            ShapeBuilder.GenerateCone(out NativeArray<float3> coneVertices, out NativeArray<int> coneIndices, Allocator.Temp);
            ShapeBuilder.GenerateArrow(out NativeArray<float3> arrowVertices, out NativeArray<int> arrowIndices, Allocator.Temp);
            ShapeBuilder.GeneratePolyCube(out NativeArray<float3> polyCubeVertices, out NativeArray<int> polyCubeIndices, Allocator.Temp);
            ShapeBuilder.GeneratePolyDisc(out NativeArray<float3> polyDiscVertices, out NativeArray<int> polyDiscIndices, Allocator.Temp);

            _InstanceBufferManager = new InstanceBufferManager();
            _InstanceBufferManager.RegisterType(DrawType.Line, "Kowloon/DebugLineDraw", typeof(GpuLine), "SHAPE_LINE", MeshTopology.Lines);
            _InstanceBufferManager.RegisterType(DrawType.Ray, "Kowloon/DebugLineDraw", typeof(GpuRay), "SHAPE_RAY", MeshTopology.Lines);
            _InstanceBufferManager.RegisterType(DrawType.Square, "Kowloon/DebugLineDraw", typeof(GpuNonUniformScale), "SHAPE_NONUNIFORM_SCALE", MeshTopology.Lines);
            _InstanceBufferManager.RegisterType(DrawType.Circle, "Kowloon/DebugLineDraw", typeof(GpuNonUniformScale), "SHAPE_NONUNIFORM_SCALE", MeshTopology.Lines);
            _InstanceBufferManager.RegisterType(DrawType.Arrow, "Kowloon/DebugLineDraw", typeof(GpuArrow), "SHAPE_ARROW", MeshTopology.Lines);
            _InstanceBufferManager.RegisterType(DrawType.Cube, "Kowloon/DebugLineDraw", typeof(GpuNonUniformScale), "SHAPE_NONUNIFORM_SCALE", MeshTopology.Lines);
            _InstanceBufferManager.RegisterType(DrawType.Sphere, "Kowloon/DebugLineDraw", typeof(GpuNonUniformScale), "SHAPE_NONUNIFORM_SCALE", MeshTopology.Lines);
            _InstanceBufferManager.RegisterType(DrawType.Capsule, "Kowloon/DebugLineDraw", typeof(GpuCapsule), "SHAPE_CAPSULE", MeshTopology.Lines);
            _InstanceBufferManager.RegisterType(DrawType.Cone, "Kowloon/DebugLineDraw", typeof(GpuCone), "SHAPE_CONE", MeshTopology.Lines);
            _InstanceBufferManager.RegisterType(DrawType.PolyTriangle, "Kowloon/DebugPolyDraw", typeof(GpuPolyTriangle), "SHAPE_POLYGON_TRIANGLE", MeshTopology.Triangles);
            _InstanceBufferManager.RegisterType(DrawType.PolyLine, "Kowloon/DebugPolyDraw", typeof(GpuPolyLine), "SHAPE_POLYGON_LINE", MeshTopology.Triangles);
            _InstanceBufferManager.RegisterType(DrawType.PolyDisc, "Kowloon/DebugPolyDraw", typeof(GpuNonUniformScale), "SHAPE_NONUNIFORM_SCALE", MeshTopology.Triangles);
            _InstanceBufferManager.RegisterType(DrawType.PolyCube, "Kowloon/DebugPolyDraw", typeof(GpuNonUniformScale), "SHAPE_NONUNIFORM_SCALE", MeshTopology.Triangles);
            _InstanceBufferManager.RegisterType(DrawType.PolyPlane, "Kowloon/DebugPolyDraw", typeof(GpuNonUniformScale), "SHAPE_POLYGON_PLANE", MeshTopology.Triangles);
            _InstanceBufferManager.RegisterType(DrawType.Text128, "Kowloon/DebugTextDraw", typeof(GpuText128), "SHAPE_TEXT_128", MeshTopology.Triangles);

            _InstanceBufferManager.SetIndexCount(DrawType.Line, 2);
            _InstanceBufferManager.SetIndexCount(DrawType.Ray, 10);
            _InstanceBufferManager.SetVertexIndexBuffers(DrawType.Square, squareVertices, squareIndices);
            _InstanceBufferManager.SetVertexIndexBuffers(DrawType.Cube, cubeVertices, cubeIndices);
            _InstanceBufferManager.SetVertexIndexBuffers(DrawType.Circle, circleVertices, circleIndices);
            _InstanceBufferManager.SetVertexIndexBuffers(DrawType.Sphere, sphereVertices, sphereIndices);
            _InstanceBufferManager.SetVertexIndexBuffers(DrawType.Capsule, capsuleVertices, capsuleIndices);
            _InstanceBufferManager.SetVertexIndexBuffers(DrawType.Arrow, arrowVertices, arrowIndices);
            _InstanceBufferManager.SetVertexIndexBuffers(DrawType.Cone, coneVertices, coneIndices);
            _InstanceBufferManager.SetIndexCount(DrawType.PolyTriangle, 3);
            _InstanceBufferManager.SetIndexCount(DrawType.PolyLine, 6);
            _InstanceBufferManager.SetVertexIndexBuffers(DrawType.PolyDisc, polyDiscVertices, polyDiscIndices);
            _InstanceBufferManager.SetVertexIndexBuffers(DrawType.PolyCube, polyCubeVertices, polyCubeIndices);
            _InstanceBufferManager.SetIndexCount(DrawType.PolyPlane, 6);
            _InstanceBufferManager.SetIndexCount(DrawType.Text128, 2 * 3 * 125); // 2 triangles per character, 3 vertices per triangle, 125 characters
            _InstanceBufferManager.SetTextureProperty(DrawType.Text128, "_MainTex", settings.FontTexture);

            _InstanceBufferManager.Initialize();

            _DebugDrawRenderPass = new DebugDrawRenderPass
            {
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing,
                PerTypeMaterials = _InstanceBufferManager.Materials,
                PerTypeIndexCounts = _InstanceBufferManager.PerTypeIndexCounts,
                PerTypeMeshTopology = _InstanceBufferManager.PerTypeMeshTopology
            };

            RenderPipelineManager.beginCameraRendering += EnqueueRenderPass;

            // Make sure UpdateWorldTimeSystem is running so we can get delta time.
            if (World.GetExistingSystemManaged<UpdateWorldTimeSystem>() == null)
            {
                UpdateWorldTimeSystem updateWorldTimeSystem = World.CreateSystemManaged<UpdateWorldTimeSystem>();
                InitializationSystemGroup initializationSystemGroup = World.GetOrCreateSystemManaged<InitializationSystemGroup>();
                initializationSystemGroup.AddSystemToUpdateList(updateWorldTimeSystem);
                initializationSystemGroup.SortSystems();
            }
        }

        protected override void OnStopRunning()
        {
            RenderPipelineManager.beginCameraRendering -= EnqueueRenderPass;

            _InstanceBufferManager?.Dispose();
            _InstanceBufferManager = null;
        }

        protected override void OnUpdate()
        {
            // Slight hack since OnUpdate gets executed at least once if we set Enabled to false in OnStartRunning
            if (!Enabled) return;

            // Invoke any outstanding debug draw events before we start reading from the DebugDrawCommandBuffer
            OnBeginUpdate.Invoke();

            DebugDrawCommandBuffer commandBuffer = DebugDrawCommandBuffer.Instance.Data;

            // Complete all jobs before reading from DebugDrawCommandBuffer and switching it to read mode.
            //  - We use CompleteAllTrackedJobs() to avoid manually tracking every job dependency that writes to the command buffer.
            //  - AtomicSafetyHandle.EnforceAllBufferJobsHaveCompleted() is not an option in shipping builds, as it’s compiled out.
            //  - This system is scheduled right after BeginPresentationEntityCommandBufferSystem to align with other structural changes.
            // Alternative Approach:
            //  - Add a JobHandle dependency to the DebugDrawCommandBuffer.
            //  - Implement a method in DebugDrawCommandBuffer to add and merge job dependencies.
            //  - Call this method for every job that gets scheduled to write to the DebugDrawCommandBuffer.
            //  - Wait until all jobs are complete before reading from the buffer.
            EntityManager.CompleteAllTrackedJobs();
            commandBuffer.SetReadOnly();

            // Before we start writing to any graphics buffer and subsequently resetting the DebugDrawCommandBuffer, we need to collect the draw counts.
            _DebugDrawRenderPass.PerTypeDrawCounts = new Dictionary<int, DrawCounts>();
            foreach (DrawType drawType in Enum.GetValues(typeof(DrawType)))
            {
                _DebugDrawRenderPass.PerTypeDrawCounts[(int)drawType] = new DrawCounts
                {
                    DepthTest = commandBuffer.GetTotalDrawCount(drawType, RenderMode.DepthTest),
                    Always = commandBuffer.GetTotalDrawCount(drawType, RenderMode.Always),
                    SeeThrough = commandBuffer.GetTotalDrawCount(drawType, RenderMode.SeeThrough),
                    Transparent = commandBuffer.GetTotalDrawCount(drawType, RenderMode.Transparent)
                };
            }

            // Prepare the instance graphics buffers for writing
            using (ProfileReCreateBuffer.Auto())
            {
                _InstanceBufferManager.LockBuffersForWrite(_DebugDrawRenderPass.PerTypeDrawCounts);
            }

            _WriteInstanceBuffersJobHandle = new WriteInstanceBuffersJob
            {
                GraphicsBufferPointers = _InstanceBufferManager.BufferPointers,
                GraphicsBufferLengths = _InstanceBufferManager.BufferLengths,
                DebugDrawCommandBuffer = commandBuffer
            }.Schedule(commandBuffer.ChunkChainCount, 1);

            _DefragBufferJobHandle = new DefragRemainingDrawsJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                ClearMask = commandBuffer.ClearMask,
                DebugDrawCommandBuffer = commandBuffer
            }.Schedule(commandBuffer.ChunkChainCount, 1, _WriteInstanceBuffersJobHandle);

            JobHandle setCommandBufferToWriteJobHandle = new SetCommandBufferToWriteJob
            {
                CommandBuffer = commandBuffer
            }.Schedule(_DefragBufferJobHandle);

            Dependency = JobHandle.CombineDependencies(_WriteInstanceBuffersJobHandle, _DefragBufferJobHandle, setCommandBufferToWriteJobHandle);
            _ReadDataOutstanding = true;
        }

        private void EnqueueRenderPass(ScriptableRenderContext context, Camera camera)
        {
            if (camera.cameraType is CameraType.Game or CameraType.SceneView)
            {
                // Wait for the read data job to complete before rendering
                if (_ReadDataOutstanding)
                {
                    _ReadDataOutstanding = false;
                    using (ProfileWaitForReadJob.Auto())
                    {
                        _DefragBufferJobHandle.Complete();
                    }

                    using (ProfileUnlockBuffer.Auto())
                    {
                        _InstanceBufferManager.UnlockBuffersAfterWrite(_DebugDrawRenderPass.PerTypeDrawCounts);
                    }
                }

                using (ProfileEnqueuePass.Auto())
                {
                    camera.GetUniversalAdditionalCameraData().scriptableRenderer.EnqueuePass(_DebugDrawRenderPass);
                }
            }
        }
    }
}