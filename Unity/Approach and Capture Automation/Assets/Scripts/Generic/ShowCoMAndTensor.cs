using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShowCoMAndTensor : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        Debug.Log(
            $"Center of mass:\t\t{rb.centerOfMass}\n" +
            $"Tensor:\t\t\t{rb.inertiaTensor}\n" +
            $"Inertia tensor rotation:\t{rb.inertiaTensorRotation.eulerAngles}"
        );
    }
}
