using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[UnityEngine.CreateAssetMenu(fileName = "FinalTransformProcessorV1", menuName = "HeartProcessors/FinalTransformProcessorV1")]
public class FinalTransformProcessorV1 : HeartsProcessorBase
{
    public override void OnUpdate(HeartsManager manager)
    {
        var inputDeps =
            JobHandle.CombineDependencies(JobHandle.CombineDependencies(manager.finalPositionsReadHandle, manager.finalPositionsWriteHandle, manager.basePositionsWriteHandle),
                                          manager.offsetPositionsWriteHandle);
        manager.finalPositionsReadHandle = manager.finalPositionsWriteHandle = new ComputePositionsJob
        {
            finalPositions  = manager.finalPositions,
            basePositions   = manager.basePositions,
            offsetPositions = manager.offsetPositions
        }.ScheduleParallel(manager.heartCount, 64, inputDeps);

        inputDeps = JobHandle.CombineDependencies(JobHandle.CombineDependencies(manager.finalRotationsReadHandle, manager.finalRotationsWriteHandle,
                                                                                manager.baseRotationsReadHandle), manager.offsetRotationsReadHandle);
        manager.finalRotationsReadHandle = manager.finalRotationsWriteHandle = new ComputeRotationsJob
        {
            finalRotations  = manager.finalRotations,
            baseRotations   = manager.baseRotations,
            offsetRotations = manager.offsetRotations
        }.ScheduleParallel(manager.heartCount, 64, inputDeps);
    }

    [BurstCompile]
    struct ComputePositionsJob : IJobFor
    {
        public NativeArray<float3>            finalPositions;
        [ReadOnly] public NativeArray<float3> basePositions;
        [ReadOnly] public NativeArray<float3> offsetPositions;

        public void Execute(int i)
        {
            finalPositions[i] = basePositions[i] + offsetPositions[i];
        }
    }

    [BurstCompile]
    struct ComputeRotationsJob : IJobFor
    {
        public NativeArray<quaternion>            finalRotations;
        [ReadOnly] public NativeArray<quaternion> baseRotations;
        [ReadOnly] public NativeArray<quaternion> offsetRotations;

        public void Execute(int i)
        {
            finalRotations[i] = math.mul(offsetRotations[i], baseRotations[i]);
        }
    }
}

