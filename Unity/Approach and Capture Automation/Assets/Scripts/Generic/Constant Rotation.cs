using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ConstantRotation : MonoBehaviour
{
    [Tooltip("Rotation speed in degrees per second.")]
    public float rotationSpeed = 45f;

    private Transform objectTransform;

    void Start()
    {
        objectTransform = this.transform;
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        objectTransform.Rotate(Vector3.forward, rotationSpeed * Time.fixedDeltaTime);
    }
}
