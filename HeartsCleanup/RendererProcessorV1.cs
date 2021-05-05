using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Jobs;

[UnityEngine.CreateAssetMenu(fileName = "RendererProcessorV1", menuName = "HeartProcessors/RendererProcessorV1")]
public class RendererProcessorV1 : HeartsProcessorBase
{
    public UnityEngine.Transform prefab;
    public int                   renderInstancesCount = 10000;

    private UnityEngine.Transform camera;
    private TransformAccessArray  transformAccessArray;

    //Cached between OnUpdate and OnLateUpdate
    private NativeArray<PriorityData> priorityDataArray;
    JobHandle                         updateHandle;

    public override void OnInitialize(HeartsManager manager)
    {
        camera               = UnityEngine.Camera.main.transform;
        transformAccessArray = new TransformAccessArray(renderInstancesCount);
        for (int i = 0; i < renderInstancesCount; i++)
        {
            transformAccessArray.Add(Instantiate(prefab));
        }

        var inputDeps                   = JobHandle.CombineDependencies(manager.baseRotationsReadHandle, manager.baseRotationsWriteHandle);
        manager.baseRotationsReadHandle = manager.baseRotationsWriteHandle = new InitializeBaseRotationsJob
        {
            baseRotations = manager.baseRotations,
            rotation      = prefab.rotation
        }.ScheduleParallel(manager.heartCount, 64, inputDeps);
    }

    public override void OnUpdate(HeartsManager manager)
    {
        updateHandle.Complete();
        priorityDataArray = new NativeArray<PriorityData>(manager.heartCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        var inputDeps     = JobHandle.CombineDependencies(manager.basePositionsWriteHandle, manager.visiblesWriteHandle);
        var jh            = new BuildPriorityDataJob
        {
            priorityDataArray = priorityDataArray,
            basePositions     = manager.basePositions,
            visibles          = manager.visibles,
            cameraPosition    = camera.position
        }.ScheduleParallel(manager.heartCount, 64, inputDeps);
        manager.basePositionsReadHandle = JobHandle.CombineDependencies(manager.baseRotationsReadHandle, jh);
        manager.visiblesReadHandle      = JobHandle.CombineDependencies(manager.visiblesReadHandle, jh);

        updateHandle = new SortJob
        {
            priorityDataArray = priorityDataArray,
            poolCount         = renderInstancesCount
        }.Schedule(jh);
    }

    public override void OnLateUpdate(HeartsManager manager)
    {
        var inputDeps = JobHandle.CombineDependencies(manager.finalPositionsWriteHandle, manager.finalRotationsWriteHandle, updateHandle);
        var jh        = new CopyTransformsToGameObjectsJob
        {
            finalPositions    = manager.finalPositions,
            finalRotations    = manager.finalRotations,
            priorityDataArray = priorityDataArray
        }.Schedule(transformAccessArray, inputDeps);
        manager.finalPositionsReadHandle = JobHandle.CombineDependencies(manager.finalPositionsReadHandle, jh);
        manager.finalRotationsReadHandle = JobHandle.CombineDependencies(manager.finalRotationsReadHandle, jh);
        updateHandle                     = priorityDataArray.Dispose(jh);
    }

    public override void OnTeardown(HeartsManager manager)
    {
        updateHandle.Complete();
        transformAccessArray.Dispose();
    }

    struct PriorityData : IComparable<PriorityData>
    {
        public int   index;
        public float distanceSq;
        public bool  visible;

        public int CompareTo(PriorityData other)
        {
            int result = -visible.CompareTo(other.visible);
            if (result == 0)
            {
                return distanceSq.CompareTo(other.distanceSq);
            }
            else
            {
                return result;
            }
        }
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
    struct BuildPriorityDataJob : IJobFor
    {
        public NativeArray<PriorityData>      priorityDataArray;
        [ReadOnly] public NativeArray<float3> basePositions;
        [ReadOnly] public NativeArray<bool>   visibles;
        public float3                         cameraPosition;

        public void Execute(int i)
        {
            priorityDataArray[i] = new PriorityData
            {
                distanceSq = math.distancesq(basePositions[i], cameraPosition),
                index      = i,
                visible    = visibles[i]
            };
        }
    }

    [BurstCompile]
    struct SortJob : IJob
    {
        public NativeArray<PriorityData> priorityDataArray;
        public int                       poolCount;

        public void Execute()
        {
            Sort(priorityDataArray);
        }

        void Sort<T>(NativeArray<T> array) where T : struct, IComparable<T>
        {
            Sort(array, 0, array.Length - 1);
        }

        void Sort<T>(NativeArray<T> array, int left, int right) where T : struct, IComparable<T>
        {
            //Only the prioritized elements need to be sorted.
            if (left >= poolCount && right >= poolCount)
                return;
            if (left < poolCount && right < poolCount)
                return;

            int i = left;
            int j = right;

            var pivot = array[(left + right) / 2];

            while (i <= j)
            {
                while (array[i].CompareTo(pivot) < 0)
                {
                    i++;
                }

                while (array[j].CompareTo(pivot) > 0)
                {
                    j--;
                }

                if (i <= j)
                {
                    var temp = array[i];
                    array[i] = array[j];
                    array[j] = temp;

                    i++;
                    j--;
                }
            }

            if (left < j)
            {
                Sort(array, left, j);
            }

            if (right > i)
            {
                Sort(array, i, right);
            }
        }
    }

    [BurstCompile]
    struct CopyTransformsToGameObjectsJob : IJobParallelForTransform
    {
        [ReadOnly] public NativeArray<PriorityData> priorityDataArray;
        [ReadOnly] public NativeArray<float3>       finalPositions;
        [ReadOnly] public NativeArray<quaternion>   finalRotations;

        public void Execute(int i, TransformAccess transform)
        {
            int index          = priorityDataArray[i].index;
            transform.position = finalPositions[index];
            transform.rotation = finalRotations[index];
        }
    }
}

