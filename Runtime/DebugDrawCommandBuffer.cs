using System.Runtime.CompilerServices;
using Unity.Assertions;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;

namespace Kowloon.DebugDraw
{
    internal struct DrawCounts
    {
        public int DepthTest;
        public int Always;
        public int SeeThrough;
        public int Transparent;
    }

    internal unsafe struct DataChunk
    {
        /// <summary> Link to the next data chunk on this chain. </summary>
        internal DataChunk* Next;

        /// <summary> Current write / read position. </summary>
        internal byte* CurrentPosition;

        /// <summary> Number of elements in this chunk. </summary>
        internal int CommandCount;

        /// <summary> Variable size data for this chunk. Always ALLOCATION_SIZE from NativeParallelBuffer. </summary>
        internal fixed byte Data[1];
    }

    /// <summary>
    /// This struct will be allocated inside NativeParallelBuffer as a memory Block. Holds the main fields related to the buffer.
    /// </summary>
    /// <remarks>
    /// The reason we are not putting the fields in this struct directly under NativeParallelWriter is that we want to be able
    /// to pass NativeParallelBuffer to other functions and jobs and want the base container to be as light as possible.
    /// </remarks>
    internal unsafe struct CommandBufferInternal
    {
        /// <summary> Size of a new chunk when it gets allocated. </summary>
        internal const int CHUNK_ALLOCATION_SIZE_BYTES = 4 * 1024; // 4 KB

        internal AllocatorManager.AllocatorHandle Allocator;

        /// <summary> Array of pointers to the current chunk for each chain. </summary>
        [NativeDisableUnsafePtrRestriction]
        internal DataChunk** CurrentChunks;

        /// <summary> Array of pointers to the first chunks in each chunk chain. </summary>
        [NativeDisableUnsafePtrRestriction]
        internal DataChunk** StartChunks;

        /// <summary> Draw counters per thread, per draw type, per render mode. </summary>
        [NativeDisableUnsafePtrRestriction]
        internal int* DrawCounters;

        /// <summary> Count of chunk chains. </summary>
        internal int ChunkChainCount;

        /// <summary> Allocation size of the main internal data. </summary>
        internal int InternalAllocationSize;

        /// <summary> Is the command buffer currently in read only or in write mode. </summary>
        internal bool IsReadOnly;

        /// <summary> Mask flags to clear this frame </summary>
        internal Mask ClearMask;

        /// <summary> Allocates a new chunk and links it to the current chunk chain for the specified index. </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static DataChunk* AllocateChunk(CommandBufferInternal* data, DataChunk* oldChunk, int chainIndex)
        {
            // Allocate memory for the new chunk plus the data space, aligned to 16.
            DataChunk* newChunk = (DataChunk*)data->Allocator.Allocate(sizeof(DataChunk) + CHUNK_ALLOCATION_SIZE_BYTES, 16);

            // Initialize the new chunk
            newChunk->Next = null;
            newChunk->CurrentPosition = newChunk->Data;
            newChunk->CommandCount = 0;

            // Link the new chunk into the chain
            if (oldChunk == null)
            {
                // This is the first chunk in the chain
                data->StartChunks[chainIndex] = newChunk;
            }
            else
            {
                // Link the old chunk to the new chunk
                oldChunk->Next = newChunk;
            }

            // Update the current chunk pointer for this chain
            data->CurrentChunks[chainIndex] = newChunk;

            return newChunk;
        }

        /// <summary> Adds a value to the NativeParallelBuffer for the specified chain index, allocating necessary memory space. </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Add<T>(CommandBufferInternal* data, int chainIndex, T value) where T : unmanaged
        {
            int size = UnsafeUtility.SizeOf<T>();
            void* destination = GetChunkAndAllocateSpace(data, chainIndex, size);
            UnsafeUtility.CopyStructureToPtr(ref value, destination);
        }

        /// <summary> Adds a block of memory of the specified size to the buffer for a given chain and returns a pointer to the allocated space. </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void* Add(CommandBufferInternal* data, int chainIndex, int size)
        {
            return GetChunkAndAllocateSpace(data, chainIndex, size);
        }

        /// <summary>
        /// Retrieves the current chunk for the specified chain and allocates a block of memory of the specified size.
        /// If no current chunk exists or the current chunk cannot fulfill the memory allocation, a new chunk is allocated.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void* GetChunkAndAllocateSpace(CommandBufferInternal* data, int chainIndex, int size)
        {
            // Safety check to ensure chainIndex is in range
            Assert.IsTrue(chainIndex < data->ChunkChainCount, "Chain index is out of range of allocated chunk chains.");

            // Check if type size exceeds chunk capacity
            Assert.IsFalse(size > CHUNK_ALLOCATION_SIZE_BYTES, $"Type size ({size} bytes) exceeds chunk capacity ({CHUNK_ALLOCATION_SIZE_BYTES} bytes).");

            // Get the current chunk for this chain
            DataChunk* chunk = data->CurrentChunks[chainIndex];

            // Allocate first chunk if none exists
            if (chunk == null)
            {
                chunk = AllocateChunk(data, null, chainIndex);
            }

            // Check if we need to allocate a new chunk
            if (chunk->CurrentPosition + size > chunk->Data + CHUNK_ALLOCATION_SIZE_BYTES)
            {
                chunk = AllocateChunk(data, chunk, chainIndex);
            }

            void* destinationPtr = chunk->CurrentPosition;

            // Update chunk state
            chunk->CurrentPosition += size;
            chunk->CommandCount++;

            return destinationPtr;
        }

        /// <summary> Calculates the memory size required to store draw counters for the specified number of threads. </summary>
        internal static int GetCounterMemorySize(int threadCount) => sizeof(int) * threadCount * EnumUtility.DrawTypeCount * EnumUtility.RenderModeCount;

        /// <summary> Calculates the memory size required to store pointers to data chunks for a specified number of threads. </summary>
        internal static int GetDataChunkPtrMemorySize(int threadCount) => sizeof(DataChunk*) * threadCount;

        /// <summary> Increments the draw counter for a specified thread, draw type, and render mode. </summary>
        internal void IncrementDrawCounter(int threadIndex, DrawType drawType, RenderMode renderMode)
        {
            DrawCounters[DebugDrawCommandBuffer.GetDrawCounterIndex(threadIndex, drawType, renderMode)]++;
        }
    }

    /// <summary>
    /// A parallel append-only buffer that can be written to from any thread.
    /// </summary>
#if false
    [NativeContainer]
#endif
    public unsafe struct DebugDrawCommandBuffer : INativeDisposable
    {
        [NativeDisableUnsafePtrRestriction]
        private CommandBufferInternal* _Internal;

        public DebugDrawCommands Draw;

#if false
        AtomicSafetyHandle m_Safety;
#endif

        /// <summary> Construct a new NativeParallelBuffer with the specified number of parallel buffer chains. </summary>
        public DebugDrawCommandBuffer(AllocatorManager.AllocatorHandle allocator)
        {
            const int mainThreadCount = 1;
            int threadCount = JobsUtility.ThreadIndexCount + mainThreadCount;

            int startChunkSize = CommandBufferInternal.GetDataChunkPtrMemorySize(threadCount);
            int currentChunkSize = CommandBufferInternal.GetDataChunkPtrMemorySize(threadCount);
            int counterSize = CommandBufferInternal.GetCounterMemorySize(threadCount);
            int internalAllocationSize = sizeof(CommandBufferInternal) + startChunkSize + currentChunkSize + counterSize;
            _Internal = (CommandBufferInternal*)allocator.Allocate(internalAllocationSize, 16);
            UnsafeUtility.MemClear(_Internal, internalAllocationSize);

            _Internal->Allocator = allocator;
            _Internal->ChunkChainCount = threadCount;
            _Internal->InternalAllocationSize = internalAllocationSize;

            // Initialize chunk pointers to null
            _Internal->CurrentChunks = (DataChunk**)((byte*)_Internal + sizeof(CommandBufferInternal));
            _Internal->StartChunks = _Internal->CurrentChunks + threadCount;
            _Internal->DrawCounters = (int*)(_Internal->StartChunks + threadCount);

            _Internal->ClearMask = Mask.None;

            Draw = new DebugDrawCommands(_Internal);

#if false
            m_Safety = AtomicSafetyHandle.Create();
#endif
        }

        public readonly bool IsCreated
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _Internal != null;
        }

        public readonly int ChunkChainCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _Internal->ChunkChainCount;
        }

        /// <summary> Get element count written to this chunk chain. </summary>
        public int GetElementCount(int chainIndex)
        {
            int count = 0;
            DataChunk* chunk = _Internal->StartChunks[chainIndex];
            while (chunk != null)
            {
                count += chunk->CommandCount;
                chunk = chunk->Next;
            }
            return count;
        }

        internal static int GetDrawCounterIndex(int threadIndex, DrawType drawType, RenderMode renderMode)
        {
            return threadIndex * EnumUtility.DrawTypeCount * EnumUtility.RenderModeCount
                   + (int)drawType * EnumUtility.RenderModeCount
                   + (int)renderMode;
        }

        internal int GetDrawCount(int threadIndex, DrawType drawType, RenderMode renderMode)
        {
            return _Internal->DrawCounters[GetDrawCounterIndex(threadIndex, drawType, renderMode)];
        }

        /// <summary> Gets the offset in the graphics buffer for the specified thread. </summary>
        internal int GetPerThreadOffset(int threadIndex, DrawType drawType, RenderMode renderMode)
        {
            int offset = 0;
            for (int i = 0; i < threadIndex; i++) offset += GetDrawCount(i, drawType, renderMode);
            return offset;
        }

        /// <summary> Gets the total draw count for the specified draw type and render mode. </summary>
        internal int GetTotalDrawCount(DrawType drawType, RenderMode renderMode)
        {
            int count = 0;
            for (int i = 0; i < _Internal->ChunkChainCount; i++)
            {
                count += GetDrawCount(i, drawType, renderMode);
            }
            return count;
        }

        /// <summary> Clear counter range for the specified chain index. </summary>
        internal void ClearDrawCountersForChain(int chainIndex)
        {
            const int elementsToClear = EnumUtility.DrawTypeCount * EnumUtility.RenderModeCount;
            const int size = sizeof(int) * elementsToClear;
            int* destination = _Internal->DrawCounters + chainIndex * elementsToClear;
            UnsafeUtility.MemClear(destination, size);
        }

        /// <summary> Increments the draw counter for a specified thread, draw type, and render mode. </summary>
        internal void IncrementDrawCounter(int threadIndex, DrawType drawType, RenderMode renderMode)
        {
            _Internal->DrawCounters[GetDrawCounterIndex(threadIndex, drawType, renderMode)]++;
        }

        /// <summary> Add mask to be cleared this frame. </summary>
        public void AddToClearMask(Mask mask)
        {
            _Internal->ClearMask |= mask;
        }

        /// <summary> Reset the clear mask. </summary>
        public void ResetClearMask()
        {
            _Internal->ClearMask = Mask.None;
        }

        public readonly Mask ClearMask
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _Internal->ClearMask;
        }


        /// <summary>
        /// Sets a new start chunk for the specified chunk chain without destroying any of the old chunks. It's the
        /// users responsibility to free the old chunk chain.
        /// </summary>
        internal void SetNewStartChunk(int chainIndex, out DataChunk* oldStartChunk)
        {
            oldStartChunk = _Internal->StartChunks[chainIndex];
            CommandBufferInternal.AllocateChunk(_Internal, null, chainIndex);
        }

        /// <summary>
        /// Add a new element to the buffer. If there is not enough space in the current chunk,
        /// a new chunk will be allocated. Use ParallelWriter for thread-safe writing.
        /// </summary>
        [WriteAccessRequired]
        public void Add<T>(int index, T value) where T : unmanaged
        {
            CommandBufferInternal.Add(_Internal, index, value);
        }

        /// <summary>
        /// Adds an entry of the specified size to the buffer and returns a pointer to the allocated memory for the specified chain.
        /// Use ParallelWriter for thread-safe writing.
        /// </summary>
        [WriteAccessRequired]
        public void* Add(int index, int size)
        {
            return CommandBufferInternal.Add(_Internal, index, size);
        }

        internal void FreeChunk(DataChunk* chunk)
        {
            AllocatorManager.Free(_Internal->Allocator, chunk, sizeof(DataChunk) + CommandBufferInternal.CHUNK_ALLOCATION_SIZE_BYTES);
        }

        public void Reset()
        {
            ResetClearMask();

            // Free all existing chunks
            for (int i = 0; i < _Internal->ChunkChainCount; i++)
            {
                DataChunk* chunk = _Internal->StartChunks[i];
                while (chunk != null)
                {
                    DataChunk* next = chunk->Next;
                    AllocatorManager.Free(_Internal->Allocator, chunk, sizeof(DataChunk) + CommandBufferInternal.CHUNK_ALLOCATION_SIZE_BYTES);
                    chunk = next;
                }
            }

            // Reset chunk pointers
            UnsafeUtility.MemClear(_Internal->CurrentChunks, CommandBufferInternal.GetDataChunkPtrMemorySize(_Internal->ChunkChainCount));
            UnsafeUtility.MemClear(_Internal->StartChunks, CommandBufferInternal.GetDataChunkPtrMemorySize(_Internal->ChunkChainCount));

            // Reset draw counters
            UnsafeUtility.MemClear(_Internal->DrawCounters, CommandBufferInternal.GetCounterMemorySize(_Internal->ChunkChainCount));
        }

        /// <summary> Wait for any jobs to complete which are writing to the CommandBuffer </summary>
        internal void WaitForWriterJobs()
        {
#if false
            AtomicSafetyHandle.EnforceAllBufferJobsHaveCompleted(m_Safety);
#endif
        }

        internal void SetReadOnly()
        {
            _Internal->IsReadOnly = true;
        }

        internal void SetWriteOnly()
        {
            _Internal->IsReadOnly = false;
        }

        /// <summary> Disposes the buffer, freeing all chunk memory and the container memory itself. </summary>
        public void Dispose()
        {
            if (!IsCreated)
            {
                return;
            }

            // Free all chunks
            for (int i = 0; i < _Internal->ChunkChainCount; i++)
            {
                DataChunk* chunk = _Internal->StartChunks[i];
                while (chunk != null)
                {
                    DataChunk* next = chunk->Next;
                    AllocatorManager.Free(_Internal->Allocator, chunk, sizeof(DataChunk) + CommandBufferInternal.CHUNK_ALLOCATION_SIZE_BYTES);
                    chunk = next;
                }
            }

            // Free the main container
            AllocatorManager.Free(_Internal->Allocator, _Internal, _Internal->InternalAllocationSize);
            _Internal = null;

#if false
            AtomicSafetyHandle.Release(m_Safety);
#endif
        }

        /// <summary> Schedules a job to dispose the buffer. </summary>
        public JobHandle Dispose(JobHandle inputDeps)
        {
            if (!IsCreated)
            {
                return inputDeps;
            }
            JobHandle jobHandle = new DebugDrawCommandBufferDisposeJob { Buffer = this }.Schedule(inputDeps);
            return jobHandle;
        }

        /// <summary> The IJob that performs the disposal of this buffer on a worker thread. </summary>
        private struct DebugDrawCommandBufferDisposeJob : IJob
        {
            public DebugDrawCommandBuffer Buffer;

            public void Execute()
            {
                Buffer.Dispose();
            }
        }

        /// <summary> Provides a parallel writer instance, allowing multiple threads to write to the buffer. </summary>
        public ParallelWriter AsParallelWriter()
        {
            return new ParallelWriter(ref this);
        }

        /// <summary> Parallel writer for the buffer. </summary>
#if false
        [NativeContainer]
        [NativeContainerIsAtomicWriteOnly]
#endif
        public struct ParallelWriter
        {
            [NativeDisableUnsafePtrRestriction]
            private readonly CommandBufferInternal* _Internal;

            public DebugDrawCommands Draw;

#if false
            internal AtomicSafetyHandle m_Safety;
#endif

            public ParallelWriter(ref DebugDrawCommandBuffer buffer)
            {
                _Internal = buffer._Internal;
                Draw = new DebugDrawCommands(_Internal);
#if false
                m_Safety = buffer.m_Safety;
#endif
            }
        }

        /// <summary> Provides an enumerator for reading data from a specific chunk chain. </summary>
        public Enumerator GetEnumerator(int index)
        {
            return new Enumerator(ref this, index);
        }

        /// <summary> Enumerator for reading data from NativeParallelBuffer. </summary>
        public struct Enumerator
        {
            [NativeDisableUnsafePtrRestriction]
            private DataChunk* _CurrentChunk;

            [NativeDisableUnsafePtrRestriction]
            private byte* _CurrentPosition;

            private int _CurrentElementCount;

            internal Enumerator(ref DebugDrawCommandBuffer buffer, int chainIndex)
            {
                _CurrentChunk = buffer._Internal->StartChunks[chainIndex];
                if (_CurrentChunk == null)
                {
                    _CurrentPosition = null;
                    _CurrentElementCount = 0;
                }
                else
                {
                    _CurrentPosition = _CurrentChunk->Data;
                    _CurrentElementCount = _CurrentChunk->CommandCount;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void UpdateState(int size)
            {
                // Advance pointer
                _CurrentPosition += size;
                _CurrentElementCount--;

                // Check if we need to advance to the next chunk
                if (_CurrentElementCount != 0) return;
                _CurrentChunk = _CurrentChunk->Next;

                // Check if we have a next chunk
                if (_CurrentChunk == null) return;

                _CurrentPosition = _CurrentChunk->Data;
                _CurrentElementCount = _CurrentChunk->CommandCount;
            }

            /// <summary> Returns a pointer to the current element without advancing. </summary>
            public T* PeekPtr<T>() where T : unmanaged
            {
                return (T*)_CurrentPosition;
            }

            /// <summary> Returns a copy of the current element without advancing. </summary>
            public T Peek<T>() where T : unmanaged
            {
                return *(T*)_CurrentPosition;
            }

            /// <summary> Returns a pointer to the current element and advances the enumerator. </summary>
            public T* NextPtr<T>() where T : unmanaged
            {
                T* ptr = (T*)_CurrentPosition;
                UpdateState(UnsafeUtility.SizeOf<T>());
                return ptr;
            }

            /// <summary> Returns a copy of the current element and advances the enumerator. </summary>
            public T Next<T>() where T : unmanaged
            {
                T value = *(T*)_CurrentPosition;
                UpdateState(UnsafeUtility.SizeOf<T>());
                return value;
            }

            /// <summary> Manually moves the reader by the specified size and decrements total elements by one element. </summary>
            public void Next(int size)
            {
                UpdateState(size);
            }

            /// <summary> Check if there are any remaining elements to read. </summary>
            public bool IsDone()
            {
                return _CurrentChunk == null || _CurrentElementCount <= 0;
            }
        }

        private class InstanceFieldKey {}

        public static readonly SharedStatic<DebugDrawCommandBuffer> Instance = SharedStatic<DebugDrawCommandBuffer>.GetOrCreate<DebugDrawCommandBuffer, InstanceFieldKey>();
    }
}