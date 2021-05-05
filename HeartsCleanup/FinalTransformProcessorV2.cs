using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[UnityEngine.CreateAssetMenu(fileName = "FinalTransformProcessorV2", menuName = "HeartProcessors/FinalTransformProcessorV2")]
public class FinalTransformProcessorV2 : HeartsProcessorBase
{
    public override void OnUpdate(HeartsManager manager)
    {
        var inputDeps =
            JobHandle.CombineDependencies(JobHandle.CombineDependencies(manager.finalPositionsReadHandle, manager.finalPositionsWriteHandle, manager.basePositionsReadHandle),
                                          manager.offsetPositionsReadHandle);
        manager.finalPositionsReadHandle = manager.finalPositionsWriteHandle = new ComputePositionsJob_Vectorized
        {
            finalPositions  = manager.finalPositions.Reinterpret<float>(12),
            basePositions   = manager.basePositions.Reinterpret<float>(12),
            offsetPositions = manager.offsetPositions.Reinterpret<float>(12)
        }.ScheduleParallel(manager.heartCount * 3, 128, inputDeps);

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
    struct ComputePositionsJob_Vectorized : IJobFor
    {
        public NativeArray<float>            finalPositions;
        [ReadOnly] public NativeArray<float> basePositions;
        [ReadOnly] public NativeArray<float> offsetPositions;

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

