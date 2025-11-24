using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FuelTankMonoBehaviour : MonoBehaviour
{
    // Note: The mass changes from fuel consumption are applied to the root Rigidbody of the satellite MassModifier

    [Header("Fuel Tank Properties")]
    [Tooltip("The starting mass of fuel in the tank in kilograms.")]
    public float startingFuelKilograms = 10f;
    [Tooltip("The remaining mass of fuel in the tank in kilograms.")]
    [ReadOnly] public float remainingFuelKilograms;

    void Start()
    {
        // Initialize fuel level
        remainingFuelKilograms = startingFuelKilograms;
    }

    // Method to consume fuel and deny an action if insufficient fuel is available
    public bool ConsumeFuel(float amountKilograms)
    {
        if (amountKilograms <= remainingFuelKilograms)
        {
            remainingFuelKilograms -= amountKilograms;
            return true; // Fuel consumption successful
        }
        else
        {
            remainingFuelKilograms = 0f; // yes, this denies the action while setting fuel to zero, technically wasteful but simple
            return false; // Not enough fuel
        }
    }

    public (bool, float[]) GetMLObservation()
    {
        // Return structure:
        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // Index:   Quantity:                               Range:                  Note:
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        // [0]      normalised remainingFuelKilograms       0.0 <= x <= 1.0         Normalized fuel level representing the remaining fuel as a fraction of starting fuel.
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        // If the fuel tank starting parameters were not set up, return a small zero-vector
        if (startingFuelKilograms == 0 || remainingFuelKilograms > startingFuelKilograms)
        {
            Debug.LogWarning("GetMLObservation was called on a FuelTankMonoBehaviour, but it wasn't set up correctly and returned (false, null).");
            return (false, null);
        }

        // Return the normalised fuel level
        return (true, new float[] { remainingFuelKilograms / startingFuelKilograms });
    }
}
