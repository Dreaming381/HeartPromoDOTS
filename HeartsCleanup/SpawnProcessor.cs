using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[UnityEngine.CreateAssetMenu(fileName = "SpawnProcessor", menuName = "HeartProcessors/SpawnProcessor")]
public class SpawnProcessor : HeartsProcessorBase
{
    public UnityEngine.Bounds bounds;

    public uint seed = 626756527;

    public override void OnInitialize(HeartsManager manager)
    {
        var inputDeps                   = JobHandle.CombineDependencies(manager.basePositionsReadHandle, manager.basePositionsWriteHandle);
        manager.basePositionsReadHandle = manager.basePositionsWriteHandle = new InitializeBasePositionsJob
        {
            basePositions = manager.basePositions,
            seed          = seed,
            bounds        = bounds
        }.Schedule(inputDeps);
    }

    [BurstCompile]
    struct InitializeBasePositionsJob : IJob
    {
        public NativeArray<float3> basePositions;
        public uint                seed;
        public UnityEngine.Bounds  bounds;

        public void Execute()
        {
            var random = new Random(seed);

            for (int i = 0; i < basePositions.Length; i++)
            {
                basePositions[i] = random.NextFloat3(bounds.min, bounds.max);
            }
        }
    }
}

