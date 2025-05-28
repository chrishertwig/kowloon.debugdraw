    using System;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.Rendering;
    using UnityEngine.Rendering.RenderGraphModule;
    using UnityEngine.Rendering.Universal;

    namespace Kowloon.DebugDraw
    {
        internal class DebugDrawRenderPass : ScriptableRenderPass
        {
            public Dictionary<int, DrawCounts> PerTypeDrawCounts;
            public Dictionary<DrawType, DrawTypeMaterials> PerTypeMaterials;
            public Dictionary<DrawType, int> PerTypeIndexCounts;
            public Dictionary<DrawType, MeshTopology> PerTypeMeshTopology;

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameContext)
            {
                UniversalResourceData frameData = frameContext.Get<UniversalResourceData>();
                UniversalCameraData cameraData = frameContext.Get<UniversalCameraData>();
                using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass("DebugDraw", out PassData passData);
                passData.PixelRect = cameraData.camera.pixelRect;
                passData.PerTypeDrawCounts = PerTypeDrawCounts;
                passData.PerTypeMaterials = PerTypeMaterials;
                passData.PerTypeIndexCounts = PerTypeIndexCounts;
                passData.PerTypeMeshTopology = PerTypeMeshTopology;
                passData.ViewMatrix = cameraData.camera.worldToCameraMatrix;
                passData.ProjectionMatrix = cameraData.camera.projectionMatrix;
                builder.SetRenderAttachment(frameData.activeColorTexture, 0);
                builder.SetRenderAttachmentDepth(frameData.activeDepthTexture, AccessFlags.ReadWrite);
                builder.SetRenderFunc((PassData data, RasterGraphContext renderGraphContext) => ExecutePass(data, renderGraphContext));
            }

            private static void ExecutePass(PassData data, RasterGraphContext rasterGraphContext)
            {
                RasterCommandBuffer cmd = rasterGraphContext.cmd;
                cmd.SetViewport(data.PixelRect);
                cmd.SetViewProjectionMatrices(data.ViewMatrix, data.ProjectionMatrix);

                for (int drawTypeIndex = 0; drawTypeIndex < EnumUtility.DrawTypeCount; drawTypeIndex++)
                {
                    for (int renderModeIndex = 0; renderModeIndex < EnumUtility.RenderModeCount; renderModeIndex++)
                    {
                        int drawCount = (RenderMode)renderModeIndex switch
                        {
                            RenderMode.DepthTest => data.PerTypeDrawCounts[drawTypeIndex].DepthTest,
                            RenderMode.Always => data.PerTypeDrawCounts[drawTypeIndex].Always,
                            RenderMode.SeeThrough => data.PerTypeDrawCounts[drawTypeIndex].SeeThrough,
                            RenderMode.Transparent => data.PerTypeDrawCounts[drawTypeIndex].Transparent,
                            _ => throw new ArgumentOutOfRangeException()
                        };
                        if (drawCount == 0) continue;
                        Material material = (RenderMode)renderModeIndex switch
                        {
                            RenderMode.DepthTest => data.PerTypeMaterials[(DrawType)drawTypeIndex].DepthTest,
                            RenderMode.Always => data.PerTypeMaterials[(DrawType)drawTypeIndex].Always,
                            RenderMode.SeeThrough => data.PerTypeMaterials[(DrawType)drawTypeIndex].SeeThrough,
                            RenderMode.Transparent => data.PerTypeMaterials[(DrawType)drawTypeIndex].Transparent,
                            _ => throw new ArgumentOutOfRangeException()
                        };

                        int indexCount = data.PerTypeIndexCounts[(DrawType)drawTypeIndex];
                        MeshTopology meshTopology = data.PerTypeMeshTopology[(DrawType)drawTypeIndex];

                        if ((RenderMode)renderModeIndex == RenderMode.SeeThrough)
                        {
                            cmd.DrawProcedural(Matrix4x4.identity, material, (int)RenderMode.SeeThrough, meshTopology, drawCount * indexCount);
                            cmd.DrawProcedural(Matrix4x4.identity, material, (int)RenderMode.DepthTest, meshTopology, drawCount * indexCount);
                        }
                        else
                        {
                            cmd.DrawProcedural(Matrix4x4.identity, material, renderModeIndex, meshTopology, drawCount * indexCount);
                        }
                    }
                }
            }

            private class PassData
            {
                public Dictionary<int, DrawCounts> PerTypeDrawCounts;
                public Dictionary<DrawType, DrawTypeMaterials> PerTypeMaterials;
                public Dictionary<DrawType, int> PerTypeIndexCounts;
                public Dictionary<DrawType, MeshTopology> PerTypeMeshTopology;
                public Rect PixelRect;
                public Matrix4x4 ProjectionMatrix;
                public Matrix4x4 ViewMatrix;
            }
        }
    }