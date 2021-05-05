using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Jobs;

[UnityEngine.CreateAssetMenu(fileName = "RendererProcessorV2", menuName = "HeartProcessors/RendererProcessorV2")]
public class RendererProcessorV2 : HeartsProcessorBase
{
    public UnityEngine.MeshRenderer prefab;
    public int                      batchSize    = 64;
    public int                      maxInstances = 1000;

    UnityEngine.ComputeBuffer localToWorldBuffer;
    int                       localToWorldPropertyId;
    UnityEngine.Mesh          mesh;
    UnityEngine.Material      material;
    float3                    scale;

    //Cached between OnUpdate and OnLateUpdate
    private NativeList<float4x4> localToWorlds;
    JobHandle                    updateHandle;

    public override void OnInitialize(HeartsManager manager)
    {
        mesh                   = prefab.GetComponent<UnityEngine.MeshFilter>().sharedMesh;
        material               = prefab.sharedMaterial;
        localToWorldPropertyId = UnityEngine.Shader.PropertyToID("_ltwBuffer");
        localToWorldBuffer     = new UnityEngine.ComputeBuffer(manager.heartCount, 64);
        material.SetBuffer(localToWorldPropertyId, localToWorldBuffer);
        scale = prefab.transform.localScale;

        var inputDeps                   = JobHandle.CombineDependencies(manager.baseRotationsReadHandle, manager.baseRotationsWriteHandle);
        manager.basePositionsReadHandle = manager.baseRotationsWriteHandle = new InitializeBaseRotationsJob
        {
            baseRotations = manager.baseRotations,
            rotation      = prefab.transform.rotation
        }.ScheduleParallel(manager.heartCount, 64, inputDeps);
    }

    public override void OnLateUpdate(HeartsManager manager)
    {
        updateHandle.Complete();
        if (localToWorlds.IsCreated)
            localToWorlds.Dispose();
        localToWorlds = new NativeList<float4x4>(1, Allocator.TempJob);
        var counts    = new NativeArray<int>((manager.heartCount / batchSize) + 1, Allocator.TempJob);

        var jh = new CountVisibleJob
        {
            counts    = counts,
            visibles  = manager.visibles,
            batchSize = batchSize
        }.ScheduleBatch(manager.heartCount, batchSize, manager.visiblesWriteHandle);

        jh = new PrefixSumAndAllocateJob
        {
            counts       = counts,
            listToResize = localToWorlds
        }.Schedule(jh);

        var inputDeps = JobHandle.CombineDependencies(manager.finalPositionsWriteHandle, manager.finalRotationsWriteHandle, jh);
        jh            = new ComputeMaticesJob
        {
            localToWorlds  = localToWorlds.AsDeferredJobArray(),
            visibles       = manager.visibles,
            finalPositions = manager.finalPositions,
            finalRotations = manager.finalRotations,
            prefixSum      = counts,
            batchSize      = batchSize,
            scale          = scale
        }.ScheduleBatch(manager.heartCount, batchSize, inputDeps);
        jh = counts.Dispose(jh);

        manager.visiblesReadHandle       = JobHandle.CombineDependencies(manager.visiblesReadHandle, jh);
        manager.finalPositionsReadHandle = JobHandle.CombineDependencies(manager.finalPositionsReadHandle, jh);
        manager.finalRotationsReadHandle = JobHandle.CombineDependencies(manager.finalRotationsReadHandle, jh);
        updateHandle                     = jh;
    }

    public override void OnRender(HeartsManager manager)
    {
        updateHandle.Complete();
        updateHandle = default;
        var ltwArray = localToWorlds.AsArray();
        if (ltwArray.Length > maxInstances)
            ltwArray = ltwArray.GetSubArray(0, maxInstances);
        localToWorldBuffer.SetData(ltwArray);
        //material.SetBuffer(localToWorldPropertyId, localToWorldBuffer);
        var bounds = new UnityEngine.Bounds(new float3(0f), new float3(1000f));
        UnityEngine.Graphics.DrawMeshInstancedProcedural(mesh,
                                                         0,
                                                         material,
                                                         bounds,
                                                         math.min(maxInstances, localToWorlds.Length));
    }

    public override void OnTeardown(HeartsManager manager)
    {
        updateHandle.Complete();
        localToWorldBuffer.Dispose();
        if (localToWorlds.IsCreated)
            localToWorlds.Dispose();
    }

    [BurstCompile]
    struct InitializeBaseRotationsJob : IJobFor
    {
        public NativeArray<quaternion> baseRotations;
        public quaternion              rotation;

        public void Execute(int i)
        {
            baseRotations[i] = rotation;
        }
    }

    [BurstCompile]
    struct CountVisibleJob : IJobParallelForBatch
    {
        [NativeDisableParallelForRestriction] public NativeArray<int> counts;
        [ReadOnly] public NativeArray<bool>                           visibles;
        public int                                                    batchSize;

        public void Execute(int startIndex, int count)
        {
            int visibleCount = 0;
            for (int i = startIndex; i < startIndex + count; i++)
            {
                visibleCount += math.select(0, 1, visibles[i]);
            }
            counts[startIndex / batchSize] = visibleCount;
        }
    }

    [BurstCompile]
    struct PrefixSumAndAllocateJob : IJob
    {
        public NativeArray<int>     counts;
        public NativeList<float4x4> listToResize;

        public void Execute()
        {
            int total = 0;
            for (int i = 0; i < counts.Length; i++)
            {
                int bucketCount  = counts[i];
                counts[i]        = total;
                total           += bucketCount;
            }
            listToResize.ResizeUninitialized(total);
        }
    }

    [BurstCompile]
    struct ComputeMaticesJob : IJobParallelForBatch
    {
        [NativeDisableParallelForRestriction] public NativeArray<float4x4> localToWorlds;
        [ReadOnly] public NativeArray<bool>                                visibles;
        [ReadOnly] public NativeArray<float3>                              finalPositions;
        [ReadOnly] public NativeArray<quaternion>                          finalRotations;
        [ReadOnly] public NativeArray<int>                                 prefixSum;
        public int                                                         batchSize;
        public float3                                                      scale;

        public void Execute(int startIndex, int count)
        {
            int destinationIndex = prefixSum[startIndex / batchSize];
            for (int i = startIndex; i < startIndex + count; i++)
            {
                if (visibles[i])
                {
                    var result                      = new float4x4(finalRotations[i], finalPositions[i]);
                    var scaleMatrix                 = float4x4.Scale(scale);
                    localToWorlds[destinationIndex] = math.mul(result, scaleMatrix);
                    destinationIndex++;
                }
            }
        }
    }
}

