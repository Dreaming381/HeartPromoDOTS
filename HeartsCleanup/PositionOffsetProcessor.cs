using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[UnityEngine.CreateAssetMenu(fileName = "PositionOffsetProcessor", menuName = "HeartProcessors/PositionOffsetProcessor")]
public class PositionOffsetProcessor : HeartsProcessorBase
{
    public float oscilationSpeed  = 0.25f;
    public float oscilationHeight = 1f;

    public uint seed = 614361786;

    public override void OnInitialize(HeartsManager manager)
    {
        var inputDeps                 = JobHandle.CombineDependencies(manager.timeOffsetsReadHandle, manager.timeOffsetsWriteHandle);
        manager.timeOffsetsReadHandle = manager.timeOffsetsWriteHandle = new InitializeTimeOffsetsJob
        {
            timeOffsets = manager.timeOffsets,
            seed        = seed
        }.Schedule(inputDeps);
    }

    public override void OnUpdate(HeartsManager manager)
    {
        var inputDeps                     = JobHandle.CombineDependencies(manager.offsetPositionsReadHandle, manager.offsetPositionsWriteHandle, manager.timeOffsetsWriteHandle);
        manager.offsetPositionsReadHandle = manager.offsetPositionsWriteHandle = new OffsetPositionsJob
        {
            offsetPositions   = manager.offsetPositions,
            timeOffsets       = manager.timeOffsets,
            oscillationSpeed  = oscilationSpeed * 2f * math.PI,
            oscillationHeight = oscilationHeight,
            time              = UnityEngine.Time.time
        }.ScheduleParallel(manager.heartCount, 32, inputDeps);
        manager.timeOffsetsReadHandle = JobHandle.CombineDependencies(manager.timeOffsetsReadHandle, manager.offsetPositionsReadHandle);
    }

    [BurstCompile]
    struct InitializeTimeOffsetsJob : IJob
    {
        public NativeArray<float> timeOffsets;
        public uint               seed;

        public void Execute()
        {
            var random = new Random(seed);

            for (int i = 0; i < timeOffsets.Length; i++)
            {
                timeOffsets[i] = random.NextFloat(0f, math.PI * 2f);
            }
        }
    }

    [BurstCompile]
    struct OffsetPositionsJob : IJobFor
    {
        public NativeArray<float3>           offsetPositions;
        [ReadOnly] public NativeArray<float> timeOffsets;
        public float                         oscillationSpeed;
        public float                         oscillationHeight;
        public float                         time;

        public void Execute(int i)
        {
            float y            = oscillationHeight * math.sin(oscillationSpeed * (timeOffsets[i] + time));
            offsetPositions[i] = new float3(0f, y, 0f);
        }
    }
}

