using System;
using System.Collections.Generic;
using Random = UnityEngine.Random;
using UnityEngine;

public class HeartPromoManager2 : MonoBehaviour
{
    public GameObject heartPrefab;
    public Bounds     bounds;
    public int        virtualHeartCount = 10000;
    public int        realHeartCount    = 1000;

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
        public Vector3 position;
        public float   distanceToCamera;
        public int     poolIndex;
        public bool    visible;

        // Prioritize hearts first by visible (visible comes before not-visible)
        // then by distance to camera (closer comes first)
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

    private Camera      mainCam;
    private HeartData[] hearts;
    private Plane[]     camPlanes = new Plane[6];

    private void Awake()
    {
        hearts = new HeartData[virtualHeartCount];
        for (int i = 0; i < realHeartCount; i++)
        {
            heartPool.Add(Instantiate(heartPrefab));
        }

        for (int i = 0; i < hearts.Length; i++)
        {
            Vector3 position;
            position.x           = Random.Range(bounds.min.x, bounds.max.x);
            position.y           = Random.Range(bounds.min.y, bounds.max.y);
            position.z           = Random.Range(bounds.min.z, bounds.max.z);
            hearts[i]            = new HeartData {
                position         = position,
                distanceToCamera = 0f,
                // There should always be {realHeartCount} number of virtual hearts
                // with valid indices
                poolIndex = i < heartPool.Count ? i : -1,
                visible   = false
            };
        }

        // Initialize the real heart positions in case the first virtual hearts are
        // immediately visible
        for (int i = 0; i < heartPool.Count; i++)
        {
            heartPool[i].transform.position = hearts[i].position;
        }

        mainCam = Camera.main;
    }

    private void Update()
    {
        GeometryUtility.CalculateFrustumPlanes(mainCam, camPlanes);
        var camPos = mainCam.transform.position;

        for (int i = 0; i < hearts.Length; i++)
        {
            var heart              = hearts[i];
            heart.distanceToCamera = Vector3.Distance(camPos, heart.position);

            // We are hardcoding extents for the heart for simplicity
            Bounds bounds = new Bounds(heart.position, new Vector3(2f, 12f, 2f));
            heart.visible = GeometryUtility.TestPlanesAABB(camPlanes, bounds);

            hearts[i] = heart;
        }

        // Sort the virtual hearts such that the ones which should map to real hearts are
        // at the beginning of the array.
        Array.Sort(hearts);

        // This is the index of the first virtual heart that should not be mapped to a real heart
        int j = heartPool.Count;

        // Iterate through all the virtual hearts that should be mapped to real hearts
        for (int i = 0; i < heartPool.Count; i++)
        {
            var heart = hearts[i];
            if (heart.poolIndex == -1)
            {
                // Find a mapped virtual heart that should no longer be mapped
                while (j < hearts.Length)
                {
                    var otherHeart = hearts[j];
                    if (otherHeart.poolIndex != -1)
                    {
                        // Remap the real heart to this unmapped virtual heart
                        heart.poolIndex                               = otherHeart.poolIndex;
                        otherHeart.poolIndex                          = -1;
                        heartPool[heart.poolIndex].transform.position = heart.position;
                        hearts[i]                                     = heart;
                        hearts[j]                                     = otherHeart;
                        j++;
                        break;
                    }
                    j++;
                }
            }
        }
    }
}

