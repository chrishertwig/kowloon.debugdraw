using Unity.Jobs;

namespace Kowloon.DebugDraw
{
    internal struct SetCommandBufferToWriteJob : IJob
    {
        internal DebugDrawCommandBuffer CommandBuffer;

        public void Execute()
        {
            CommandBuffer.SetWriteOnly();
            CommandBuffer.ResetClearMask();
        }
    }
}