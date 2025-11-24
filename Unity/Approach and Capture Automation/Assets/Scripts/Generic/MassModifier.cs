using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MassModifier : MonoBehaviour
{
    // Impact the satellites mass by consuming fuel
    // Limits: Only looks for one FuelTankMonoBehaviour in the children of the root object

    public bool startingRigidBodyMassIncludesFuel = true;
    private Rigidbody rb;
    private float dryMass;
    private FuelTankMonoBehaviour ft;

    void Start()
    {
        rb = transform.root.GetComponent<Rigidbody>();
        ft = transform.root.GetComponentInChildren<FuelTankMonoBehaviour>();
        dryMass = startingRigidBodyMassIncludesFuel ? rb.mass - ft.startingFuelKilograms : rb.mass;
        if (rb == null || ft == null)
        {
            Debug.LogError("MassModifier could not find the required Rigidbody or FuelTankMonoBehaviour components in object hierarchy.");
            enabled = false;
            return;
        }
        //Debug.Log($"[MassModifier] Initialized with total mass {rb.mass}kg. Dry: {dryMass}kg, Fuel: {ft.startingFuelKilograms}kg.");
    }
    void Update()
    {
        rb.mass = dryMass + ft.remainingFuelKilograms;
    }
}
