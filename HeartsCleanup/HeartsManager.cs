using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;

public class HeartsManager : MonoBehaviour
{
    public int                       heartCount = 100000;
    public List<HeartsProcessorBase> heartProcessors;

    public NativeArray<float3> basePositions { get; private set; }
    public NativeArray<float3> offsetPositions { get; private set; }
    public NativeArray<float3> finalPositions { get; private set; }
    public NativeArray<quaternion> baseRotations { get; private set; }
    public NativeArray<quaternion> offsetRotations { get; private set; }
    public NativeArray<quaternion> finalRotations { get; private set; }
    public NativeArray<float> timeOffsets { get; private set; }
    public NativeArray<bool> visibles { get; private set; }

    public JobHandle basePositionsReadHandle;
    public JobHandle basePositionsWriteHandle;
    public JobHandle offsetPositionsReadHandle;
    public JobHandle offsetPositionsWriteHandle;
    public JobHandle finalPositionsReadHandle;
    public JobHandle finalPositionsWriteHandle;
    public JobHandle baseRotationsReadHandle;
    public JobHandle baseRotationsWriteHandle;
    public JobHandle offsetRotationsReadHandle;
    public JobHandle offsetRotationsWriteHandle;
    public JobHandle finalRotationsReadHandle;
    public JobHandle finalRotationsWriteHandle;
    public JobHandle timeOffsetsReadHandle;
    public JobHandle timeOffsetsWriteHandle;
    public JobHandle visiblesReadHandle;
    public JobHandle visiblesWriteHandle;

    void Awake()
    {
        basePositions   = new NativeArray<float3>(heartCount, Allocator.Persistent);
        offsetPositions = new NativeArray<float3>(heartCount, Allocator.Persistent);
        finalPositions  = new NativeArray<float3>(heartCount, Allocator.Persistent);
        baseRotations   = new NativeArray<quaternion>(heartCount, Allocator.Persistent);
        offsetRotations = new NativeArray<quaternion>(heartCount, Allocator.Persistent);
        finalRotations  = new NativeArray<quaternion>(heartCount, Allocator.Persistent);
        timeOffsets     = new NativeArray<float>(heartCount, Allocator.Persistent);
        visibles        = new NativeArray<bool>(heartCount, Allocator.Persistent);
    }

    private void Start()
    {
        foreach (var processor in heartProcessors)
        {
            Profiler.BeginSample(processor.name);
            processor.OnInitialize(this);
            Profiler.EndSample();
        }
        JobHandle.ScheduleBatchedJobs();
    }

    void Update()
    {
        CompleteAllJobs();
        foreach (var processor in heartProcessors)
        {
            Profiler.BeginSample(processor.name);
            processor.OnUpdate(this);
            Profiler.EndSample();
        }
        JobHandle.ScheduleBatchedJobs();
    }

    private void LateUpdate()
    {
        foreach (var processor in heartProcessors)
        {
            Profiler.BeginSample(processor.name);
            processor.OnLateUpdate(this);
            Profiler.EndSample();
        }
        JobHandle.ScheduleBatchedJobs();

        foreach (var processor in heartProcessors)
        {
            Profiler.BeginSample(processor.name);
            processor.OnRender(this);
            Profiler.EndSample();
        }
        JobHandle.ScheduleBatchedJobs();
    }

    /*private void OnPreRender()
       {
        foreach (var processor in heartProcessors)
        {
            Profiler.BeginSample(processor.name);
            processor.OnRender(this);
            Profiler.EndSample();
        }
        JobHandle.ScheduleBatchedJobs();
       }*/

    private void CompleteAllJobs()
    {
        var handles = new NativeArray<JobHandle>(16, Allocator.Temp);
        handles[0]  = basePositionsReadHandle;
        handles[1]  = basePositionsWriteHandle;
        handles[2]  = offsetPositionsReadHandle;
        handles[3]  = offsetPositionsWriteHandle;
        handles[4]  = finalPositionsReadHandle;
        handles[5]  = finalPositionsWriteHandle;
        handles[6]  = baseRotationsReadHandle;
        handles[7]  = baseRotationsWriteHandle;
        handles[8]  = offsetRotationsReadHandle;
        handles[9]  = offsetRotationsWriteHandle;
        handles[10] = finalRotationsReadHandle;
        handles[11] = finalRotationsWriteHandle;
        handles[12] = timeOffsetsReadHandle;
        handles[13] = timeOffsetsWriteHandle;
        handles[14] = visiblesReadHandle;
        handles[15] = visiblesWriteHandle;
        JobHandle.CompleteAll(handles);
        //handles.Dispose();

        basePositionsReadHandle    = default;
        basePositionsWriteHandle   = default;
        offsetPositionsReadHandle  = default;
        offsetPositionsWriteHandle = default;
        finalPositionsReadHandle   = default;
        finalPositionsWriteHandle  = default;
        baseRotationsReadHandle    = default;
        baseRotationsWriteHandle   = default;
        offsetRotationsReadHandle  = default;
        offsetRotationsWriteHandle = default;
        finalRotationsReadHandle   = default;
        finalRotationsWriteHandle  = default;
        timeOffsetsReadHandle      = default;
        timeOffsetsWriteHandle     = default;
        visiblesReadHandle         = default;
        visiblesWriteHandle        = default;
    }

    private void OnDestroy()
    {
        CompleteAllJobs();

        foreach (var processor in heartProcessors)
        {
            Profiler.BeginSample(processor.name);
            processor.OnTeardown(this);
            Profiler.EndSample();
        }

        CompleteAllJobs();

        basePositions.Dispose();
        offsetPositions.Dispose();
        finalPositions.Dispose();
        baseRotations.Dispose();
        offsetRotations.Dispose();
        finalRotations.Dispose();
        timeOffsets.Dispose();
        visibles.Dispose();
    }
}

