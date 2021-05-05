using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[UnityEngine.CreateAssetMenu(fileName = "RotationOffsetProcessor", menuName = "HeartProcessors/RotationOffsetProcessor")]
public class RotationOffsetProcessor : HeartsProcessorBase
{
    public float rotationSpeed = 0.33f;

    public uint seed = 1904672389;

    public override void OnInitialize(HeartsManager manager)
    {
        var inputDeps                     = JobHandle.CombineDependencies(manager.offsetRotationsReadHandle, manager.offsetRotationsWriteHandle);
        manager.offsetRotationsReadHandle = manager.offsetRotationsWriteHandle = new InitializeRotationOffsetsJob
        {
            rotationOffsets = manager.offsetRotations,
            seed            = seed
        }.Schedule(inputDeps);
    }

    public override void OnUpdate(HeartsManager manager)
    {
        var inputDeps                     = JobHandle.CombineDependencies(manager.offsetRotationsReadHandle, manager.offsetRotationsWriteHandle);
        manager.offsetRotationsReadHandle = manager.offsetRotationsWriteHandle = new OffsetRotationJob
        {
            offsetRotations = manager.offsetRotations,
            rotationsSpeed  = rotationSpeed * 2f * math.PI,
            deltaTime       = UnityEngine.Time.deltaTime
        }.ScheduleParallel(manager.heartCount, 32, inputDeps);
    }

    [BurstCompile]
    struct InitializeRotationOffsetsJob : IJob
    {
        public NativeArray<quaternion> rotationOffsets;
        public uint                    seed;

        public void Execute()
        {
            var random = new Random(seed);

            for (int i = 0; i < rotationOffsets.Length; i++)
            {
                rotationOffsets[i] = quaternion.Euler(0f, random.NextFloat(0f, 2f * math.PI), 0f);
            }
        }
    }

    [BurstCompile]
    struct OffsetRotationJob : IJobFor
    {
        public NativeArray<quaternion> offsetRotations;
        public float                   rotationsSpeed;
        public float                   deltaTime;

        public void Execute(int i)
        {
            offsetRotations[i] = math.mul(offsetRotations[i], quaternion.Euler(0f, rotationsSpeed * deltaTime, 0f));
        }
    }
}

