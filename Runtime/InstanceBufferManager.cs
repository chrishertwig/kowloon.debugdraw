using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Assertions;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Kowloon.DebugDraw
{
    internal struct DrawTypeBufferPointers
    {
        public unsafe void* DepthTest;
        public unsafe void* Always;
        public unsafe void* SeeThrough;
        public unsafe void* Transparent;
    }

    /// <summary> Buffer lengths in byte. Only used for safety checks. </summary>
    internal struct DrawTypeBufferLengths
    {
        public int DepthTest;
        public int Always;
        public int SeeThrough;
        public int Transparent;
    }

    internal struct DrawTypeMaterials
    {
        public Material DepthTest;
        public Material Always;
        public Material SeeThrough;
        public Material Transparent;
    }

    internal class InstanceBufferManager : IDisposable
    {
        internal struct DrawTypeInstanceBuffers
        {
            public GraphicsBuffer DepthTest;
            public GraphicsBuffer Always;
            public GraphicsBuffer SeeThrough;
            public GraphicsBuffer Transparent;
        }

        private struct DrawTypeVertexIndexBuffers
        {
            public GraphicsBuffer Vertex;
            public GraphicsBuffer Index;
        }

        private static readonly int InstanceBufferID = Shader.PropertyToID("instanceBuffer");
        private static readonly int VertexBufferID = Shader.PropertyToID("vertexBuffer");
        private static readonly int IndexBufferID = Shader.PropertyToID("indexBuffer");
        private static readonly int IndexCountID = Shader.PropertyToID("indexCount");
        private static readonly int VertexCountID = Shader.PropertyToID("vertexCount");

        internal readonly Dictionary<DrawType, DrawTypeMaterials> Materials = new();
        internal readonly Dictionary<DrawType, int> PerTypeIndexCounts = new();
        internal readonly Dictionary<DrawType, MeshTopology> PerTypeMeshTopology = new();
        private readonly Dictionary<DrawType, DrawTypeInstanceBuffers> _InstanceBuffers = new();
        private readonly Dictionary<DrawType, DrawTypeVertexIndexBuffers> _VertexIndexBuffers = new();
        private readonly Dictionary<DrawType, Type> _DrawTypeToElementType = new();
        private readonly Dictionary<DrawType, MethodInfo> _LockBufferForWriteMethods = new();
        private readonly Dictionary<DrawType, MethodInfo> _GetUnsafePtrMethods = new();
        private readonly Dictionary<DrawType, MethodInfo> _UnlockBufferAfterWriteMethods = new();

        internal NativeArray<DrawTypeBufferPointers> BufferPointers;
        internal NativeArray<DrawTypeBufferLengths> BufferLengths;

        private bool _IsInitialized;
        private int _TypeCount;

        public void RegisterType(DrawType drawType, string shaderName, Type gpuInstanceType, string keyword, MeshTopology meshTopology)
        {
            Assert.IsFalse(_IsInitialized, "Can not register additional types after InstanceBufferManager is initialized.");
            Assert.IsFalse(Materials.ContainsKey(drawType), $"DrawType {drawType} is already registered.");

            Materials[drawType] = new DrawTypeMaterials
            {
                DepthTest = CreateMaterial(shaderName, "DepthTest", keyword),
                Always = CreateMaterial(shaderName, "Always", keyword),
                SeeThrough = CreateMaterial(shaderName, "SeeThrough", keyword),
                Transparent = CreateMaterial(shaderName, "Transparent", keyword)
            };
            _InstanceBuffers[drawType] = new DrawTypeInstanceBuffers
            {
                DepthTest = null,
                Always = null,
                SeeThrough = null,
                Transparent = null
            };
            _DrawTypeToElementType[drawType] = gpuInstanceType;
            _LockBufferForWriteMethods[drawType] = GetGenericMethod(typeof(GraphicsBuffer), nameof(GraphicsBuffer.LockBufferForWrite), gpuInstanceType);
            _GetUnsafePtrMethods[drawType] = GetGenericMethod(typeof(InstanceBufferManager), nameof(InstanceBufferManager.GetUnsafePtrAsIntPtr), gpuInstanceType, BindingFlags.NonPublic | BindingFlags.Static);
            _UnlockBufferAfterWriteMethods[drawType] = GetGenericMethod(typeof(GraphicsBuffer), nameof(GraphicsBuffer.UnlockBufferAfterWrite), gpuInstanceType);
            PerTypeMeshTopology[drawType] = meshTopology;
            _TypeCount++;
        }

        public void SetIndexCount(DrawType drawType, int indexCount)
        {
            PerTypeIndexCounts[drawType] = indexCount;

            for (int renderModeIndex = 0; renderModeIndex < EnumUtility.RenderModeCount; renderModeIndex++)
            {
                Material material = (RenderMode)renderModeIndex switch
                {
                    RenderMode.DepthTest => Materials[drawType].DepthTest,
                    RenderMode.Always => Materials[drawType].Always,
                    RenderMode.SeeThrough => Materials[drawType].SeeThrough,
                    RenderMode.Transparent => Materials[drawType].Transparent,
                    _ => throw new ArgumentOutOfRangeException()
                };
                material.SetInteger(IndexCountID, indexCount);
            }
        }

        public void SetVertexIndexBuffers(DrawType drawType, NativeArray<float3> vertices, NativeArray<int> indices)
        {
            _VertexIndexBuffers[drawType] = new DrawTypeVertexIndexBuffers
            {
                Vertex = new GraphicsBuffer(GraphicsBuffer.Target.Structured, vertices.Length, UnsafeUtility.SizeOf<float3>()),
                Index = new GraphicsBuffer(GraphicsBuffer.Target.Structured, indices.Length, UnsafeUtility.SizeOf<int>())
            };
            _VertexIndexBuffers[drawType].Vertex.SetData(vertices);
            _VertexIndexBuffers[drawType].Index.SetData(indices);
            PerTypeIndexCounts[drawType] = indices.Length;

            for (int renderModeType = 0; renderModeType < EnumUtility.RenderModeCount; renderModeType++)
            {
                Material material = (RenderMode)renderModeType switch
                {
                    RenderMode.DepthTest => Materials[drawType].DepthTest,
                    RenderMode.Always => Materials[drawType].Always,
                    RenderMode.SeeThrough => Materials[drawType].SeeThrough,
                    RenderMode.Transparent => Materials[drawType].Transparent,
                    _ => throw new ArgumentOutOfRangeException()
                };
                material.SetBuffer(VertexBufferID, _VertexIndexBuffers[drawType].Vertex);
                material.SetBuffer(IndexBufferID, _VertexIndexBuffers[drawType].Index);
                material.SetInteger(VertexCountID, vertices.Length);
                material.SetInteger(IndexCountID, indices.Length);
            }
        }

        public void SetTextureProperty(DrawType drawType, string propertyName, Texture texture)
        {
            Materials[drawType].DepthTest.SetTexture(propertyName, texture);
            Materials[drawType].Always.SetTexture(propertyName, texture);
            Materials[drawType].SeeThrough.SetTexture(propertyName, texture);
            Materials[drawType].Transparent.SetTexture(propertyName, texture);
        }

        private static unsafe IntPtr GetUnsafePtrAsIntPtr<T>(NativeArray<T> nativeArray) where T : struct
        {
            return (IntPtr)nativeArray.GetUnsafePtr();
        }

        private static MethodInfo GetGenericMethod(Type type, string methodName, Type elementType, BindingFlags bindingFlags = BindingFlags.Default)
        {
            MethodInfo method = bindingFlags == BindingFlags.Default ? type.GetMethod(methodName) : type.GetMethod(methodName, bindingFlags);
            if (method == null)
            {
                Debug.LogError($"Failed to find method {methodName} on type {type}");
                return null;
            }

            MethodInfo genericMethod = method.MakeGenericMethod(elementType);
            return genericMethod;
        }

        public void Initialize()
        {
            BufferPointers = new NativeArray<DrawTypeBufferPointers>(_TypeCount, Allocator.Persistent);
            BufferLengths = new NativeArray<DrawTypeBufferLengths>(_TypeCount, Allocator.Persistent);
            _IsInitialized = true;
        }

        public unsafe void LockBuffersForWrite(in Dictionary<int, DrawCounts> drawCounts)
        {
            Assert.IsTrue(_IsInitialized, "InstanceBufferManager is not initialized.");

            foreach (DrawType drawType in Enum.GetValues(typeof(DrawType)))
            {
                int countDepthTest = drawCounts[(int)drawType].DepthTest;
                int countAlways = drawCounts[(int)drawType].Always;
                int countSeeThrough = drawCounts[(int)drawType].SeeThrough;
                int countTransparent = drawCounts[(int)drawType].Transparent;

                // Skip types that are not registered
                if (!_DrawTypeToElementType.TryGetValue(drawType, out Type elementType)) continue;

                DrawTypeInstanceBuffers buffers = _InstanceBuffers[drawType];
                RecreateInstanceBuffer(ref buffers.DepthTest, countDepthTest, UnsafeUtility.SizeOf(elementType));
                RecreateInstanceBuffer(ref buffers.Always, countAlways, UnsafeUtility.SizeOf(elementType));
                RecreateInstanceBuffer(ref buffers.SeeThrough, countSeeThrough, UnsafeUtility.SizeOf(elementType));
                RecreateInstanceBuffer(ref buffers.Transparent, countTransparent, UnsafeUtility.SizeOf(elementType));
                _InstanceBuffers[drawType] = buffers;

                if (_InstanceBuffers[drawType].DepthTest != null)
                    Materials[drawType].DepthTest.SetBuffer(InstanceBufferID, _InstanceBuffers[drawType].DepthTest);

                if (_InstanceBuffers[drawType].Always != null)
                    Materials[drawType].Always.SetBuffer(InstanceBufferID, _InstanceBuffers[drawType].Always);

                if (_InstanceBuffers[drawType].SeeThrough != null)
                    Materials[drawType].SeeThrough.SetBuffer(InstanceBufferID, _InstanceBuffers[drawType].SeeThrough);

                if (_InstanceBuffers[drawType].Transparent != null)
                    Materials[drawType].Transparent.SetBuffer(InstanceBufferID, _InstanceBuffers[drawType].Transparent);

                BufferPointers[(int)drawType] = new DrawTypeBufferPointers
                {
                    DepthTest = countDepthTest > 0 ? LockBufferForWriteAndGetUnsafePtr(drawType, RenderMode.DepthTest, countDepthTest) : null,
                    Always = countAlways > 0 ? LockBufferForWriteAndGetUnsafePtr(drawType, RenderMode.Always, countAlways) : null,
                    SeeThrough = countSeeThrough > 0 ? LockBufferForWriteAndGetUnsafePtr(drawType, RenderMode.SeeThrough, countSeeThrough) : null,
                    Transparent = countTransparent > 0 ? LockBufferForWriteAndGetUnsafePtr(drawType, RenderMode.Transparent, countTransparent) : null
                };

                BufferLengths[(int)drawType] = new DrawTypeBufferLengths
                {
                    DepthTest = countDepthTest * UnsafeUtility.SizeOf(elementType),
                    Always = countAlways * UnsafeUtility.SizeOf(elementType),
                    SeeThrough = countSeeThrough * UnsafeUtility.SizeOf(elementType),
                    Transparent = countTransparent * UnsafeUtility.SizeOf(elementType)
                };
            }
        }

        private unsafe void* LockBufferForWriteAndGetUnsafePtr(DrawType drawType, RenderMode renderMode, int drawCount)
        {
            GraphicsBuffer buffer = renderMode switch
            {
                RenderMode.DepthTest => _InstanceBuffers[drawType].DepthTest,
                RenderMode.Always => _InstanceBuffers[drawType].Always,
                RenderMode.SeeThrough => _InstanceBuffers[drawType].SeeThrough,
                RenderMode.Transparent => _InstanceBuffers[drawType].Transparent,
                _ => throw new ArgumentOutOfRangeException(nameof(renderMode), renderMode, null)
            };

            // Call LockBufferForWrite<T> to get a NativeArray<T>
            object nativeArray = _LockBufferForWriteMethods[drawType].Invoke(buffer, new object[] { 0, drawCount });

            // Call GetUnsafePtr<T> to get the pointer to the NativeArray<T>
            object result = _GetUnsafePtrMethods[drawType].Invoke(null, new[] { nativeArray });
            IntPtr ptr = (IntPtr)result;
            void* unsafePtr = (void*)ptr;

            return unsafePtr;
        }

        public void UnlockBuffersAfterWrite(in Dictionary<int, DrawCounts> drawCounts)
        {
            Assert.IsTrue(_IsInitialized, "InstanceBufferManager is not initialized.");

            foreach (DrawType drawType in Enum.GetValues(typeof(DrawType)))
            {
                int countDepthTest = drawCounts[(int)drawType].DepthTest;
                int countAlways = drawCounts[(int)drawType].Always;
                int countSeeThrough = drawCounts[(int)drawType].SeeThrough;
                int countTransparent = drawCounts[(int)drawType].Transparent;

                // Call UnlockBufferAfterWrite<T> to unlock GraphicsBuffer
                if (countDepthTest > 0) _UnlockBufferAfterWriteMethods[drawType].Invoke(_InstanceBuffers[drawType].DepthTest, new object[] { countDepthTest });
                if (countAlways > 0) _UnlockBufferAfterWriteMethods[drawType].Invoke(_InstanceBuffers[drawType].Always, new object[] { countAlways });
                if (countSeeThrough > 0) _UnlockBufferAfterWriteMethods[drawType].Invoke(_InstanceBuffers[drawType].SeeThrough, new object[] { countSeeThrough });
                if (countTransparent > 0) _UnlockBufferAfterWriteMethods[drawType].Invoke(_InstanceBuffers[drawType].Transparent, new object[] { countTransparent });
            }
        }

        private static Material CreateMaterial(string shaderName, string bufferType, string keyword)
        {
            Shader shader = Shader.Find(shaderName);
            Material material = new Material(shader)
            {
                name = $"{shaderName}_{bufferType}"
            };
            material.hideFlags = HideFlags.DontSave;
            if (!string.IsNullOrEmpty(keyword))
            {
                material.EnableKeyword(keyword);
            }
            return material;
        }

        private static void RecreateInstanceBuffer(ref GraphicsBuffer buffer, int count, int stride)
        {
            if (count == 0)
            {
                ReleaseBuffer(ref buffer);
                return;
            }

            bool updateBuffer = buffer == null || buffer.count != count;
            if (!updateBuffer) return;

            if (buffer != null)
            {
                ReleaseBuffer(ref buffer);
            }

            buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.LockBufferForWrite, count, stride);
        }

        private static void ReleaseBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null) return;
            buffer.Release();
            buffer = null;
        }

        public void Dispose()
        {
            BufferPointers.Dispose();
            BufferLengths.Dispose();

            foreach (DrawTypeInstanceBuffers buffers in _InstanceBuffers.Values)
            {
                buffers.DepthTest?.Release();
                buffers.Always?.Release();
                buffers.SeeThrough?.Release();
                buffers.Transparent?.Release();
            }

            foreach (DrawTypeVertexIndexBuffers buffers in _VertexIndexBuffers.Values)
            {
                buffers.Vertex?.Release();
                buffers.Index?.Release();
            }
        }
    }
}