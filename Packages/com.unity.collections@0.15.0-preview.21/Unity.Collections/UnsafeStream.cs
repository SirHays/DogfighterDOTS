using Unity.Burst;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using UnityEngine.Assertions;

namespace Unity.Collections.LowLevel.Unsafe
{
    [BurstCompatible]
    internal unsafe struct UnsafeStreamBlock
    {
        internal UnsafeStreamBlock* Next;
        internal fixed byte Data[1];
    }

    [BurstCompatible]
    internal unsafe struct UnsafeStreamRange
    {
        internal UnsafeStreamBlock* Block;
        internal int OffsetInFirstBlock;
        internal int ElementCount;

        /// One byte past the end of the last byte written
        internal int LastOffset;
        internal int NumberOfBlocks;
    }

    [BurstCompatible]
    internal unsafe struct UnsafeStreamBlockData
    {
        internal const int AllocationSize = 4 * 1024;
        internal Allocator Allocator;

        internal UnsafeStreamBlock** Blocks;
        internal int BlockCount;

        internal UnsafeStreamBlock* Free;

        internal UnsafeStreamRange* Ranges;
        internal int RangeCount;

        internal UnsafeStreamBlock* Allocate(UnsafeStreamBlock* oldBlock, int threadIndex)
        {
            Assert.IsTrue(threadIndex < BlockCount && threadIndex >= 0);

            UnsafeStreamBlock* block = (UnsafeStreamBlock*)Memory.Unmanaged.Allocate(AllocationSize, 16, Allocator);
            block->Next = null;

            if (oldBlock == null)
            {
                // Append our new block in front of the previous head.
                block->Next = Blocks[threadIndex];
                Blocks[threadIndex] = block;
            }
            else
            {
                oldBlock->Next = block;
            }

            return block;
        }
    }

    /// <summary>
    /// A deterministic data streaming supporting parallel reading and parallel writings, without any thread safety check features.
    /// Allows you to write different types or arrays into a single stream.
    /// </summary>
    [BurstCompatible]
    public unsafe struct UnsafeStream
        : INativeDisposable
    {
        [NativeDisableUnsafePtrRestriction]
        internal UnsafeStreamBlockData* m_Block;
        internal Allocator m_Allocator;

        /// <summary>
        /// Constructs a new UnsafeStream using the specified type of memory allocation.
        /// </summary>
        /// <param name="foreachCount"></param>
        /// <param name="allocator"></param>
        public UnsafeStream(int foreachCount, Allocator allocator)
        {
            AllocateBlock(out this, allocator);
            AllocateForEach(foreachCount);
        }

        /// <summary>
        /// Schedule job to construct a new UnsafeStream using the specified type of memory allocation.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="stream"></param>
        /// <param name="forEachCountFromList"></param>
        /// <param name="dependency">All jobs spawned will depend on this JobHandle.</param>
        /// <param name="allocator">A member of the
        /// [Unity.Collections.Allocator](https://docs.unity3d.com/ScriptReference/Unity.Collections.Allocator.html) enumeration.</param>
        /// <returns></returns>
        [BurstCompatible(GenericTypeArguments = new[] { typeof(int) }, RequiredUnityDefine = "UNITY_2020_2_OR_NEWER") /* Due to job scheduling on 2020.1 using statics */]
        public static JobHandle ScheduleConstruct<T>(out UnsafeStream stream, NativeList<T> forEachCountFromList, JobHandle dependency, Allocator allocator)
            where T : struct
        {
            AllocateBlock(out stream, allocator);
            var jobData = new ConstructJobList { List = forEachCountFromList.GetUnsafeList(), Container = stream };
            return jobData.Schedule(dependency);
        }

        /// <summary>
        /// Schedule job to construct a new UnsafeStream using the specified type of memory allocation.
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="lengthFromIndex0"></param>
        /// <param name="dependency">All jobs spawned will depend on this JobHandle.</param>
        /// <param name="allocator">A member of the
        /// [Unity.Collections.Allocator](https://docs.unity3d.com/ScriptReference/Unity.Collections.Allocator.html) enumeration.</param>
        /// <returns></returns>
        [BurstCompatible(RequiredUnityDefine = "UNITY_2020_2_OR_NEWER") /* Due to job scheduling on 2020.1 using statics */]
        public static JobHandle ScheduleConstruct(out UnsafeStream stream, NativeArray<int> lengthFromIndex0, JobHandle dependency, Allocator allocator)
        {
            AllocateBlock(out stream, allocator);
            var jobData = new ConstructJob { Length = lengthFromIndex0, Container = stream };
            return jobData.Schedule(dependency);
        }

        internal static void AllocateBlock(out UnsafeStream stream, Allocator allocator)
        {
            int blockCount = JobsUtility.MaxJobThreadCount;

            int allocationSize = sizeof(UnsafeStreamBlockData) + sizeof(UnsafeStreamBlock*) * blockCount;
            byte* buffer = (byte*)Memory.Unmanaged.Allocate(allocationSize, 16, allocator);
            UnsafeUtility.MemClear(buffer, allocationSize);

            var block = (UnsafeStreamBlockData*)buffer;

            stream.m_Block = block;
            stream.m_Allocator = allocator;

            block->Allocator = allocator;
            block->BlockCount = blockCount;
            block->Blocks = (UnsafeStreamBlock**)(buffer + sizeof(UnsafeStreamBlockData));

            block->Ranges = null;
            block->RangeCount = 0;
        }

        internal void AllocateForEach(int forEachCount)
        {
            long allocationSize = sizeof(UnsafeStreamRange) * forEachCount;
            m_Block->Ranges = (UnsafeStreamRange*)Memory.Unmanaged.Allocate(allocationSize, 16, m_Allocator);
            m_Block->RangeCount = forEachCount;
            UnsafeUtility.MemClear(m_Block->Ranges, allocationSize);
        }

        /// <summary>
        /// Reports whether container is empty.
        /// </summary>
        /// <returns>True if this container empty.</returns>
        public bool IsEmpty()
        {
            if (!IsCreated)
            {
                return true;
            }

            for (int i = 0; i != m_Block->RangeCount; i++)
            {
                if (m_Block->Ranges[i].ElementCount > 0)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Reports whether memory for the container is allocated.
        /// </summary>
        /// <value>True if this container object's internal storage has been allocated.</value>
        /// <remarks>
        /// Note that the container storage is not created if you use the default constructor. You must specify
        /// at least an allocation type to construct a usable container.
        ///
        /// *Warning:* the `IsCreated` property can't be used to determine whether a copy of a container is still valid.
        /// If you dispose any copy of the container, the container storage is deallocated. However, the properties of
        /// the other copies of the container (including the original) are not updated. As a result the `IsCreated` property
        /// of the copies still return `true` even though the container storage has been deallocated.
        /// </remarks>
        public bool IsCreated => m_Block != null;

        /// <summary>
        /// </summary>
        public int ForEachCount { get { return m_Block->RangeCount; } }

        /// <summary>
        /// Returns reader instance.
        /// </summary>
        /// <returns>Reader instance</returns>
        public Reader AsReader()
        {
            return new Reader(ref this);
        }

        /// <summary>
        /// Returns writer instance.
        /// </summary>
        /// <returns>Writer instance</returns>
        public Writer AsWriter()
        {
            return new Writer(ref this);
        }

        /// <summary>
        /// The current number of items in the container.
        /// </summary>
        /// <returns>The item count.</returns>
        public int Count()
        {
            int itemCount = 0;

            for (int i = 0; i != m_Block->RangeCount; i++)
            {
                itemCount += m_Block->Ranges[i].ElementCount;
            }

            return itemCount;
        }

        /// <summary>
        /// Copies stream data into NativeArray.
        /// </summary>
        /// <typeparam name="T">The type of value.</typeparam>
        /// <param name="allocator">A member of the
        /// [Unity.Collections.Allocator](https://docs.unity3d.com/ScriptReference/Unity.Collections.Allocator.html) enumeration.</param>
        /// <returns>A new NativeArray, allocated with the given strategy and wrapping the stream data.</returns>
        /// <remarks>The array is a copy of stream data.</remarks>
        [BurstCompatible(GenericTypeArguments = new[] { typeof(int) })]
        public NativeArray<T> ToNativeArray<T>(Allocator allocator) where T : struct
        {
            var array = new NativeArray<T>(Count(), allocator, NativeArrayOptions.UninitializedMemory);
            var reader = AsReader();

            int offset = 0;
            for (int i = 0; i != reader.ForEachCount; i++)
            {
                reader.BeginForEachIndex(i);
                int rangeItemCount = reader.RemainingItemCount;
                for (int j = 0; j < rangeItemCount; ++j)
                {
                    array[offset] = reader.Read<T>();
                    offset++;
                }
                reader.EndForEachIndex();
            }

            return array;
        }

        void Deallocate()
        {
            if (m_Block == null)
            {
                return;
            }

            for (int i = 0; i != m_Block->BlockCount; i++)
            {
                UnsafeStreamBlock* block = m_Block->Blocks[i];
                while (block != null)
                {
                    UnsafeStreamBlock* next = block->Next;
                    Memory.Unmanaged.Free(block, m_Allocator);
                    block = next;
                }
            }

            Memory.Unmanaged.Free(m_Block->Ranges, m_Allocator);
            Memory.Unmanaged.Free(m_Block, m_Allocator);
            m_Block = null;
            m_Allocator = Allocator.None;
        }

        /// <summary>
        /// Disposes of this stream and deallocates its memory immediately.
        /// </summary>
        public void Dispose()
        {
            Deallocate();
        }

        /// <summary>
        /// Safely disposes of this container and deallocates its memory when the jobs that use it have completed.
        /// </summary>
        /// <remarks>You can call this function dispose of the container immediately after scheduling the job. Pass
        /// the [JobHandle](https://docs.unity3d.com/ScriptReference/Unity.Jobs.JobHandle.html) returned by
        /// the [Job.Schedule](https://docs.unity3d.com/ScriptReference/Unity.Jobs.IJobExtensions.Schedule.html)
        /// method using the `jobHandle` parameter so the job scheduler can dispose the container after all jobs
        /// using it have run.</remarks>
        /// <param name="inputDeps">All jobs spawned will depend on this JobHandle.</param>
        /// <returns>A new job handle containing the prior handles as well as the handle for the job that deletes
        /// the container.</returns>
        [BurstCompatible(RequiredUnityDefine = "UNITY_2020_2_OR_NEWER") /* Due to job scheduling on 2020.1 using statics */]
        public JobHandle Dispose(JobHandle inputDeps)
        {
            var jobHandle = new DisposeJob { Container = this }.Schedule(inputDeps);

            m_Block = null;

            return jobHandle;
        }

        [BurstCompile]
        struct DisposeJob : IJob
        {
            public UnsafeStream Container;

            public void Execute()
            {
                Container.Deallocate();
            }
        }

        [BurstCompile]
        struct ConstructJobList : IJob
        {
            public UnsafeStream Container;

            [ReadOnly]
            [NativeDisableUnsafePtrRestriction]
            public UnsafeList* List;

            public void Execute()
            {
                Container.AllocateForEach(List->Length);
            }
        }

        [BurstCompile]
        struct ConstructJob : IJob
        {
            public UnsafeStream Container;

            [ReadOnly]
            public NativeArray<int> Length;

            public void Execute()
            {
                Container.AllocateForEach(Length[0]);
            }
        }

        /// <summary>
        /// </summary>
        [BurstCompatible]
        public unsafe struct Writer
        {
            [NativeDisableUnsafePtrRestriction]
            internal UnsafeStreamBlockData* m_BlockStream;

            [NativeDisableUnsafePtrRestriction]
            UnsafeStreamBlock* m_CurrentBlock;

            [NativeDisableUnsafePtrRestriction]
            byte* m_CurrentPtr;

            [NativeDisableUnsafePtrRestriction]
            byte* m_CurrentBlockEnd;

            internal int m_ForeachIndex;
            int m_ElementCount;

            [NativeDisableUnsafePtrRestriction]
            UnsafeStreamBlock* m_FirstBlock;

            int m_FirstOffset;
            int m_NumberOfBlocks;

            [NativeSetThreadIndex]
            int m_ThreadIndex;

            internal Writer(ref UnsafeStream stream)
            {
                m_BlockStream = stream.m_Block;
                m_ForeachIndex = int.MinValue;
                m_ElementCount = -1;
                m_CurrentBlock = null;
                m_CurrentBlockEnd = null;
                m_CurrentPtr = null;
                m_FirstBlock = null;
                m_NumberOfBlocks = 0;
                m_FirstOffset = 0;
                m_ThreadIndex = 0;
            }

            /// <summary>
            /// </summary>
            public int ForEachCount { get { return m_BlockStream->RangeCount; } }

            /// <summary>
            /// Begin reading data at the iteration index.
            /// </summary>
            /// <param name="foreachIndex"></param>
            /// <remarks>BeginForEachIndex must always be called balanced by a EndForEachIndex.</remarks>
            public void BeginForEachIndex(int foreachIndex)
            {
                m_ForeachIndex = foreachIndex;
                m_ElementCount = 0;
                m_NumberOfBlocks = 0;
                m_FirstBlock = m_CurrentBlock;
                m_FirstOffset = (int)(m_CurrentPtr - (byte*)m_CurrentBlock);
            }

            /// <summary>
            /// Ensures that all data has been read for the active iteration index.
            /// </summary>
            /// <remarks>EndForEachIndex must always be called balanced by a BeginForEachIndex.</remarks>
            public void EndForEachIndex()
            {
                m_BlockStream->Ranges[m_ForeachIndex].ElementCount = m_ElementCount;
                m_BlockStream->Ranges[m_ForeachIndex].OffsetInFirstBlock = m_FirstOffset;
                m_BlockStream->Ranges[m_ForeachIndex].Block = m_FirstBlock;

                m_BlockStream->Ranges[m_ForeachIndex].LastOffset = (int)(m_CurrentPtr - (byte*)m_CurrentBlock);
                m_BlockStream->Ranges[m_ForeachIndex].NumberOfBlocks = m_NumberOfBlocks;
            }

            /// <summary>
            /// Write data.
            /// </summary>
            /// <typeparam name="T">The type of value.</typeparam>
            /// <param name="value">Value to write.</param>
            [BurstCompatible(GenericTypeArguments = new[] { typeof(int) })]
            public void Write<T>(T value) where T : struct
            {
                ref T dst = ref Allocate<T>();
                dst = value;
            }

            /// <summary>
            /// Allocate space for data.
            /// </summary>
            /// <typeparam name="T">The type of value.</typeparam>
            /// <returns>Reference to allocated space for data.</returns>
            [BurstCompatible(GenericTypeArguments = new[] { typeof(int) })]
            public ref T Allocate<T>() where T : struct
            {
                int size = UnsafeUtility.SizeOf<T>();
                return ref UnsafeUtility.AsRef<T>(Allocate(size));
            }

            /// <summary>
            /// Allocate space for data.
            /// </summary>
            /// <param name="size">Size in bytes.</param>
            /// <returns>Pointer to allocated space for data.</returns>
            public byte* Allocate(int size)
            {
                byte* ptr = m_CurrentPtr;
                m_CurrentPtr += size;

                if (m_CurrentPtr > m_CurrentBlockEnd)
                {
                    UnsafeStreamBlock* oldBlock = m_CurrentBlock;

                    m_CurrentBlock = m_BlockStream->Allocate(oldBlock, m_ThreadIndex);
                    m_CurrentPtr = m_CurrentBlock->Data;

                    if (m_FirstBlock == null)
                    {
                        m_FirstOffset = (int)(m_CurrentPtr - (byte*)m_CurrentBlock);
                        m_FirstBlock = m_CurrentBlock;
                    }
                    else
                    {
                        m_NumberOfBlocks++;
                    }

                    m_CurrentBlockEnd = (byte*)m_CurrentBlock + UnsafeStreamBlockData.AllocationSize;
                    ptr = m_CurrentPtr;
                    m_CurrentPtr += size;
                }

                m_ElementCount++;

                return ptr;
            }
        }

        /// <summary>
        /// </summary>
        [BurstCompatible]
        public unsafe struct Reader
        {
            [NativeDisableUnsafePtrRestriction]
            internal UnsafeStreamBlockData* m_BlockStream;

            [NativeDisableUnsafePtrRestriction]
            internal UnsafeStreamBlock* m_CurrentBlock;

            [NativeDisableUnsafePtrRestriction]
            internal byte* m_CurrentPtr;

            [NativeDisableUnsafePtrRestriction]
            internal byte* m_CurrentBlockEnd;

            internal int m_RemainingItemCount;
            internal int m_LastBlockSize;

            internal Reader(ref UnsafeStream stream)
            {
                m_BlockStream = stream.m_Block;
                m_CurrentBlock = null;
                m_CurrentPtr = null;
                m_CurrentBlockEnd = null;
                m_RemainingItemCount = 0;
                m_LastBlockSize = 0;
            }

            /// <summary>
            /// Begin reading data at the iteration index.
            /// </summary>
            /// <param name="foreachIndex"></param>
            /// <remarks>BeginForEachIndex must always be called balanced by a EndForEachIndex.</remarks>
            /// <returns>The number of elements at this index.</returns>
            public int BeginForEachIndex(int foreachIndex)
            {
                m_RemainingItemCount = m_BlockStream->Ranges[foreachIndex].ElementCount;
                m_LastBlockSize = m_BlockStream->Ranges[foreachIndex].LastOffset;

                m_CurrentBlock = m_BlockStream->Ranges[foreachIndex].Block;
                m_CurrentPtr = (byte*)m_CurrentBlock + m_BlockStream->Ranges[foreachIndex].OffsetInFirstBlock;
                m_CurrentBlockEnd = (byte*)m_CurrentBlock + UnsafeStreamBlockData.AllocationSize;

                return m_RemainingItemCount;
            }

            /// <summary>
            /// Ensures that all data has been read for the active iteration index.
            /// </summary>
            /// <remarks>EndForEachIndex must always be called balanced by a BeginForEachIndex.</remarks>
            public void EndForEachIndex()
            {
            }

            /// <summary>
            /// Returns for each count.
            /// </summary>
            public int ForEachCount { get { return m_BlockStream->RangeCount; } }

            /// <summary>
            /// Returns remaining item count.
            /// </summary>
            public int RemainingItemCount { get { return m_RemainingItemCount; } }

            /// <summary>
            /// Returns pointer to data.
            /// </summary>
            /// <param name="size">Size in bytes.</param>
            /// <returns>Pointer to data.</returns>
            public byte* ReadUnsafePtr(int size)
            {
                m_RemainingItemCount--;

                byte* ptr = m_CurrentPtr;
                m_CurrentPtr += size;

                if (m_CurrentPtr > m_CurrentBlockEnd)
                {
                    m_CurrentBlock = m_CurrentBlock->Next;
                    m_CurrentPtr = m_CurrentBlock->Data;

                    m_CurrentBlockEnd = (byte*)m_CurrentBlock + UnsafeStreamBlockData.AllocationSize;

                    ptr = m_CurrentPtr;
                    m_CurrentPtr += size;
                }

                return ptr;
            }

            /// <summary>
            /// Read data.
            /// </summary>
            /// <typeparam name="T">The type of value.</typeparam>
            /// <returns>Reference to data.</returns>
            [BurstCompatible(GenericTypeArguments = new[] { typeof(int) })]
            public ref T Read<T>() where T : struct
            {
                int size = UnsafeUtility.SizeOf<T>();
                return ref UnsafeUtility.AsRef<T>(ReadUnsafePtr(size));
            }

            /// <summary>
            /// Peek into data.
            /// </summary>
            /// <typeparam name="T">The type of value.</typeparam>
            /// <returns>Reference to data.</returns>
            [BurstCompatible(GenericTypeArguments = new[] { typeof(int) })]
            public ref T Peek<T>() where T : struct
            {
                int size = UnsafeUtility.SizeOf<T>();

                byte* ptr = m_CurrentPtr;
                if (ptr + size > m_CurrentBlockEnd)
                {
                    ptr = m_CurrentBlock->Next->Data;
                }

                return ref UnsafeUtility.AsRef<T>(ptr);
            }

            /// <summary>
            /// The current number of items in the container.
            /// </summary>
            /// <returns>The item count.</returns>
            public int Count()
            {
                int itemCount = 0;
                for (int i = 0; i != m_BlockStream->RangeCount; i++)
                {
                    itemCount += m_BlockStream->Ranges[i].ElementCount;
                }

                return itemCount;
            }
        }
    }
}
