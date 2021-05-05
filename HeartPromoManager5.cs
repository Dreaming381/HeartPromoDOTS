using System;
using System.Collections.Generic;
using Random = UnityEngine.Random;
using UnityEngine;

using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

public class HeartPromoManager5 : MonoBehaviour
{
    public GameObject heartPrefab;
    public Bounds     bounds;
    public int        virtualHeartCount = 100000;
    public int        realHeartCount    = 3000;

    private List<GameObject> heartPool = new List<GameObject>();

    private void OnEnable()
    {
        foreach (var heart in heartPool)
        {
            heart.SetActive(true);
        }
    }

    private void OnDisable()
    {
        foreach (var heart in heartPool)
        {
            //Check exists to avoid error when exiting playmode.
            if (heart != null)
                heart.SetActive(false);
        }
    }

    struct HeartData : IComparable<HeartData>
    {
        public float3 position;
        public float  distanceToCamera;
        public int    poolIndex;
        public bool   visible;

        public int CompareTo(HeartData other)
        {
            int result = -visible.CompareTo(other.visible);
            if (result == 0)
            {
                return distanceToCamera.CompareTo(other.distanceToCamera);
            }
            else
            {
                return result;
            }
        }
    }

    struct PoolRecord
    {
        public float3 position;
        public bool   writeThisFrame;
    }

    // This is new!
    struct HeartPromoLogicData
    {
        public float oscilationSpeed;
        public float oscilationHeight;

        public float rotationSpeed;
    }

    // And so is this!
    struct HeartPromoLogicTransformValues
    {
        public float3     position;
        public quaternion rotation;
    }

    [BurstCompile]
    struct PoolRecordResetJob : IJobFor
    {
        public NativeArray<PoolRecord> poolRecords;

        public void Execute(int i)
        {
            var record            = poolRecords[i];
            record.writeThisFrame = false;
            poolRecords[i]        = record;
        }
    }

    [BurstCompile]
    struct HeartCullJob : IJobFor
    {
        [ReadOnly] public NativeArray<float4> planes;
        public NativeArray<HeartData>         hearts;

        public float3 camPos;

        public void Execute(int i)
        {
            var heart              = hearts[i];
            heart.distanceToCamera = math.distancesq(camPos, heart.position);  //Optimization: distanceSq is good enough for our needs

            heart.visible = TestPlanes(heart.position);
            hearts[i]     = heart;
        }

        bool TestPlanes(float3 position)
        {
            float4 p      = new float4(position, 1f);
            bool   inside = true;
            for (int i = 0; i < 6; i++)
            {
                inside &= math.dot(planes[i], p) > -6f;
            }
            return inside;
        }
    }

    [BurstCompile]
    struct HeartSortJob : IJob
    {
        public NativeArray<HeartData> hearts;
        public int                    poolCount;

        public void Execute()
        {
            Sort(hearts);
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
    struct HeartSwapJob : IJob
    {
        public NativeArray<HeartData>  hearts;
        public NativeArray<PoolRecord> poolRecords;

        public void Execute()
        {
            int j = poolRecords.Length;
            for (int i = 0; i < poolRecords.Length; i++)
            {
                var heart = hearts[i];
                if (heart.poolIndex == -1)
                {
                    while (j < hearts.Length)
                    {
                        var otherHeart = hearts[j];
                        if (otherHeart.poolIndex != -1)
                        {
                            heart.poolIndex              = otherHeart.poolIndex;
                            otherHeart.poolIndex         = -1;
                            poolRecords[heart.poolIndex] = new PoolRecord
                            {
                                position       = heart.position,
                                writeThisFrame = true
                            };
                            hearts[i] = heart;
                            hearts[j] = otherHeart;
                            j++;
                            break;
                        }
                        j++;
                    }
                }
            }
        }
    }

    [BurstCompile]
    struct HeartPromoLogicJob : IJobFor
    {
        [ReadOnly] public NativeArray<HeartPromoLogicData> data;
        public NativeArray<HeartPromoLogicTransformValues> transforms;

        public float time;
        public float deltaTime;

        public void Execute(int i)
        {
            float y       = data[i].oscilationHeight * math.sin(data[i].oscilationSpeed * time);
            var   tf      = transforms[i];
            tf.position   = new float3(0f, y, 0f);
            tf.rotation   = math.mul(quaternion.Euler(0f, math.radians(data[i].rotationSpeed * deltaTime), 0f), tf.rotation);
            transforms[i] = tf;
        }
    }

    private Camera                                      mainCam;
    private NativeArray<HeartData>                      hearts;
    private NativeArray<PoolRecord>                     poolRecords;
    private Plane[]                                     camPlanes = new Plane[6];
    private NativeArray<float4>                         planes;
    private NativeArray<HeartPromoLogicData>            promoLogicData;
    private NativeArray<HeartPromoLogicTransformValues> promoLogicTransformValues;
    // Caching the children is a massive optimization.
    // Iterating through all the real Transforms is slow.
    // That's why the writeThisFrame bool in past iterations
    // was so important.
    private Transform[] childTransforms;

    // We have two independent job chains now.
    JobHandle jobHandleVirtual;  // Virtual mapping of hearts to GameObjects
    JobHandle jobHandlePool;  // Updating transforms of child GameObjects in pool

    private void Awake()
    {
        hearts      = new NativeArray<HeartData>(virtualHeartCount, Allocator.Persistent);
        poolRecords = new NativeArray<PoolRecord>(realHeartCount, Allocator.Persistent);
        planes      = new NativeArray<float4>(6, Allocator.Persistent);

        promoLogicData            = new NativeArray<HeartPromoLogicData>(poolRecords.Length, Allocator.Persistent);
        promoLogicTransformValues = new NativeArray<HeartPromoLogicTransformValues>(poolRecords.Length, Allocator.Persistent);
        childTransforms           = new Transform[poolRecords.Length];

        for (int i = 0; i < poolRecords.Length; i++)
        {
            heartPool.Add(Instantiate(heartPrefab));
        }

        for (int i = 0; i < hearts.Length; i++)
        {
            Vector3 position;
            position.x = Random.Range(bounds.min.x, bounds.max.x);
            position.y = Random.Range(bounds.min.y, bounds.max.y);
            position.z = Random.Range(bounds.min.z, bounds.max.z);
            hearts[i]  = new HeartData
            {
                position         = position,
                distanceToCamera = 0f,
                poolIndex        = i < heartPool.Count ? i : -1,
                visible          = false
            };
        }

        // There's a lot more to do now, since we are now replacing the HeartPromoLogic's Update method with a job.
        // We now need to copy that script's data and disable it.
        for (int i = 0; i < heartPool.Count; i++)
        {
            heartPool[i].transform.position = hearts[i].position;
            var logic                       = heartPool[i].transform.GetChild(0).GetComponent<HeartPromoLogic>();
            promoLogicData[i]               = new HeartPromoLogicData
            {
                oscilationHeight = logic.oscilationHeight,
                oscilationSpeed  = logic.oscilationSpeed,
                rotationSpeed    = logic.rotationSpeed
            };
            promoLogicTransformValues[i] = new HeartPromoLogicTransformValues
            {
                position = float3.zero,
                rotation = logic.transform.localRotation
            };
            childTransforms[i] = logic.transform;
            logic.enabled      = false;
        }

        mainCam = Camera.main;
    }

    private void Update()
    {
        GeometryUtility.CalculateFrustumPlanes(mainCam, camPlanes);
        var camPos = mainCam.transform.position;

        for (int i = 0; i < 6; i++)
        {
            planes[i] = new float4(camPlanes[i].normal, camPlanes[i].distance);
        }

        var resetJobHandle = new PoolRecordResetJob
        {
            poolRecords = poolRecords
        }.Schedule(poolRecords.Length, default);
        var cullingSortingJobHandle = new HeartCullJob
        {
            camPos = camPos,
            hearts = hearts,
            planes = planes
        }.ScheduleParallel(hearts.Length, 16, default);

        //Reuse handles
        cullingSortingJobHandle = new HeartSortJob
        {
            hearts    = hearts,
            poolCount = poolRecords.Length
        }.Schedule(cullingSortingJobHandle);

        jobHandleVirtual = JobHandle.CombineDependencies(resetJobHandle, cullingSortingJobHandle);

        jobHandleVirtual = new HeartSwapJob
        {
            hearts      = hearts,
            poolRecords = poolRecords
        }.Schedule(jobHandleVirtual);

        jobHandlePool = new HeartPromoLogicJob
        {
            data       = promoLogicData,
            transforms = promoLogicTransformValues,
            time       = Time.unscaledTime,
            deltaTime  = Time.unscaledDeltaTime
        }.ScheduleParallel(promoLogicData.Length, 8, default);

        JobHandle.ScheduleBatchedJobs();
    }

    private void LateUpdate()
    {
        jobHandlePool.Complete();
        ApplyChildTransforms();
        jobHandleVirtual.Complete();
        ApplyRecords();
    }

    void ApplyRecords()
    {
        for (int i = 0; i < poolRecords.Length; i++)
        {
            if (poolRecords[i].writeThisFrame)
            {
                heartPool[i].transform.position = poolRecords[i].position;
            }
        }
    }

    void ApplyChildTransforms()
    {
        for (int i = 0; i < poolRecords.Length; i++)
        {
            var tf           = childTransforms[i];
            tf.localPosition = promoLogicTransformValues[i].position;
            tf.localRotation = promoLogicTransformValues[i].rotation;
        }
    }

    private void OnDestroy()
    {
        hearts.Dispose();
        poolRecords.Dispose();
        planes.Dispose();
        promoLogicData.Dispose();
        promoLogicTransformValues.Dispose();
    }
}

