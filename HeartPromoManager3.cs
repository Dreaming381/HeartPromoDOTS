using System;
using System.Collections.Generic;
using Random = UnityEngine.Random;
using UnityEngine;

// New DOTS namespaces
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

public class HeartPromoManager3 : MonoBehaviour
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
        // float3 comes from the new Unity Mathematics library.
        // Most operations are defined in the static `math` class.
        // Yes, a lot of things are all lower case in this library.
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

    // Each real heart has an associated record.
    // These records can be written to in jobs.
    struct PoolRecord
    {
        public float3 position;
        public bool   writeThisFrame;
    }

    // This attribute is usually added live during demos.
    [BurstCompile]
    struct HeartsPoolUpdateJob : IJob
    {
        // By marking this array as [ReadOnly] we tell Unity's
        // native memory safety system that we are only reading
        // this data.
        [ReadOnly] public NativeArray<float4> planes;
        public NativeArray<HeartData>         hearts;
        public NativeArray<PoolRecord>        poolRecords;

        // This variable is copied by value and will be destroyed
        // when the job finishes.
        public float3 camPos;

        // This is the main entry point for IJob.
        public void Execute()
        {
            // We have to reset the writeThisFrame value prior to each update.
            for (int i = 0; i < poolRecords.Length; i++)
            {
                var record            = poolRecords[i];
                record.writeThisFrame = false;
                poolRecords[i]        = record;
            }

            for (int i = 0; i < hearts.Length; i++)
            {
                var heart = hearts[i];

                // Optimization: distanceSq is good enough for our needs
                heart.distanceToCamera = math.distancesq(camPos, heart.position);
                heart.visible          = TestPlanes(heart.position);
                hearts[i]              = heart;
            }

            // We can't use System.Sort so we use a custom sort instead.
            // The Unity Collections package also has a sort function,
            // but we will be optimizing this version for our specific
            // use case.
            Sort(hearts);

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
                            heart.poolIndex      = otherHeart.poolIndex;
                            otherHeart.poolIndex = -1;

                            // Instead of writing directly to the transform, we write
                            // to this record.
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

        bool TestPlanes(float3 position)
        {
            // We have swapped from an AABB test to a sphere test
            // with a fixed radius of 6
            float4 p      = new float4(position, 1f);
            bool   inside = true;
            for (int i = 0; i < 6; i++)
            {
                inside &= math.dot(planes[i], p) > -6f;
            }
            return inside;
        }

        // This is a common quicksort implementation you can find floating around the internet
        void Sort<T>(NativeArray<T> array) where T : struct, IComparable<T>
        {
            Sort(array, 0, array.Length - 1);
        }

        void Sort<T>(NativeArray<T> array, int left, int right) where T : struct, IComparable<T>
        {
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

    private Camera                  mainCam;
    private NativeArray<HeartData>  hearts;
    private NativeArray<PoolRecord> poolRecords;
    private Plane[]                 camPlanes = new Plane[6];
    private NativeArray<float4>     planes;

    bool      useWorkerThread = false;
    JobHandle jobHandle;

    private void Awake()
    {
        hearts      = new NativeArray<HeartData>(virtualHeartCount, Allocator.Persistent);
        poolRecords = new NativeArray<PoolRecord>(realHeartCount, Allocator.Persistent);
        planes      = new NativeArray<float4>(6, Allocator.Persistent);

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

        for (int i = 0; i < heartPool.Count; i++)
        {
            heartPool[i].transform.position = hearts[i].position;
        }

        mainCam = Camera.main;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.J))
        {
            useWorkerThread = !useWorkerThread;
        }

        GeometryUtility.CalculateFrustumPlanes(mainCam, camPlanes);
        var camPos = mainCam.transform.position;

        // Convert our planes into a more convenient type
        for (int i = 0; i < 6; i++)
        {
            planes[i] = new float4(camPlanes[i].normal, camPlanes[i].distance);
        }

        var job = new HeartsPoolUpdateJob
        {
            camPos      = camPos,
            hearts      = hearts,
            poolRecords = poolRecords,
            planes      = planes,
        };

        if (useWorkerThread)
        {
            jobHandle = job.Schedule();

            // Despite scheduling the job, we need to tell Unity to kick them off.
            // This is fairly expensive, so it is a good practice to schedule
            // multiple jobs and kick them all off at once with a single
            // ScheduleBatchedJobs() call.
            JobHandle.ScheduleBatchedJobs();
        }
        else
        {
            job.Run();

            // After running our job, our records are ready to be written back to
            // the GameObjects
            ApplyRecords();
        }
    }

    private void LateUpdate()
    {
        if (useWorkerThread)
        {
            // We have to tell the main thread to wait for the job to finish before
            // continuing.
            jobHandle.Complete();
            ApplyRecords();
        }
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

    // NativeArrays are not tracked by the Garbage Collector,
    // so we need to Dispose them manually.
    private void OnDestroy()
    {
        hearts.Dispose();
        poolRecords.Dispose();
        planes.Dispose();
    }
}

