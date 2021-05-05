using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[UnityEngine.CreateAssetMenu(fileName = "CullProcessor", menuName = "HeartProcessors/CullProcessor")]
public class CullProcessor : HeartsProcessorBase
{
    public float boundingRadius = 5f;

    private UnityEngine.Camera  camera;
    private UnityEngine.Plane[] camPlanes = new UnityEngine.Plane[6];
    private NativeArray<float4> planes;

    public override void OnInitialize(HeartsManager manager)
    {
        camera = UnityEngine.Camera.main;
        planes = new NativeArray<float4>(6, Allocator.Persistent);
    }

    public override void OnUpdate(HeartsManager manager)
    {
        UnityEngine.GeometryUtility.CalculateFrustumPlanes(camera, camPlanes);

        for (int i = 0; i < 6; i++)
        {
            planes[i] = new float4(camPlanes[i].normal, camPlanes[i].distance);
        }

        var inputDeps              = JobHandle.CombineDependencies(manager.basePositionsWriteHandle, manager.visiblesReadHandle, manager.visiblesWriteHandle);
        manager.visiblesReadHandle = manager.visiblesWriteHandle = new CullJob
        {
            visible        = manager.visibles,
            basePositions  = manager.basePositions,
            planes         = planes,
            boundingRadius = boundingRadius
        }.ScheduleParallel(manager.heartCount, 32, inputDeps);
        manager.basePositionsReadHandle = JobHandle.CombineDependencies(manager.basePositionsReadHandle, manager.visiblesReadHandle);
    }

    public override void OnTeardown(HeartsManager manager)
    {
        planes.Dispose();
    }

    [BurstCompile]
    struct CullJob : IJobFor
    {
        public NativeArray<bool>              visible;
        [ReadOnly] public NativeArray<float3> basePositions;
        [ReadOnly] public NativeArray<float4> planes;
        public float                          boundingRadius;

        public void Execute(int i)
        {
            visible[i] = TestPlanes(basePositions[i]);
        }

        bool TestPlanes(float3 position)
        {
            float4 p      = new float4(position, 1f);
            bool   inside = true;
            for (int i = 0; i < 6; i++)
            {
                inside &= math.dot(planes[i], p) > -boundingRadius;
            }
            return inside;
        }
    }
}

