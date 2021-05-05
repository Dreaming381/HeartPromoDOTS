using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HeartPromoLogic : MonoBehaviour
{
    public float oscilationSpeed;
    public float oscilationHeight;

    public float rotationSpeed;

    void Update()
    {
        float y                 = oscilationHeight * Mathf.Sin(oscilationSpeed * Time.unscaledTime);
        transform.localPosition = new Vector3(0, y, 0);
        transform.Rotate(0, rotationSpeed * Time.unscaledDeltaTime, 0, Space.World);
    }
}

