# Kowloon DebugDraw

Debug draw package used by Kowloon project.

### Goals
- High performance draw commands to have minimal impact on frame time even when issuing tens to hundreds of thousands commands a frame.
- Execute draw commands from any thread at any time with minimal to no setup.

### Solution
- To store the debug draw commands a native parallel buffer is utilized, which keeps dedicated memory blocks per thread. Once a block runs out of memory additional blocks are located for the thread.
- Once a frame these draw commands are passed to actual graphics buffers used for drawing the shapes.
- Each shape / draw mode combination is a single drawcall for its instances.

### Future work
- Currently, no frustum culling or the likes is happening. This could be added as an optional argument for draw commands to support an even larger amount of draws.
- The biggest bottleneck right now is the parallel writing to the graphics instance buffers, which can take over 2ms when reaching 100,000+ commands.

## Usage

### Main thread draws
```csharp
using Kowloon.DebugDraw;

[WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)] 
public partial struct DebugDrawProfilerSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        Draw.Line(start, end, color, duration, renderMode, depthBias);
    }    
}
```

### Parallel draws
```csharp
using Kowloon.DebugDraw;

[WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)] 
public partial struct DebugDrawProfilerSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        // Pass parallel writer instance to scheduled parallel job.
        LineDrawingJob job = new()
        {
            DebugDrawBuffer = DebugDrawCommandBuffer.Instance.Data.AsParallelWriter()
        };
        state.Dependency = job.ScheduleParallel(length, 128, state.Dependency);
    }    
}

[BurstCompile]
public struct LineDrawingJob : IJobFor
{
    public DebugDrawCommandBuffer.ParallelWriter DebugDrawBuffer;

    public void Execute(int index)
    {
        // Issue draw command
        DebugDrawBuffer.Draw.Line(start, end, color, duration, renderMode, depthBias);
    }
}
```