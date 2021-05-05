using System;
using System.Collections.Generic;
using Random = UnityEngine.Random;
using UnityEngine;

public class HeartPromoManager1 : MonoBehaviour
{
    public GameObject heartPrefab;
    public Bounds     bounds;
    public int        heartCount = 1000;

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

    private void Awake()
    {
        for (int i = 0; i < heartCount; i++)
        {
            Vector3 position;
            position.x = Random.Range(bounds.min.x, bounds.max.x);
            position.y = Random.Range(bounds.min.y, bounds.max.y);
            position.z = Random.Range(bounds.min.z, bounds.max.z);
            var heart  = Instantiate(heartPrefab, position, Quaternion.identity);
            heartPool.Add(heart);
        }
    }
}

