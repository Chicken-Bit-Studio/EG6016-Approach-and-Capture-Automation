using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IgnoreCollision : MonoBehaviour
{
    [Header("Pair 1")]
    public Collider collider1;
    public Collider collider2;
    [Header("Pair 2")]
    public Collider collider3;
    public Collider collider4;


    void Start()
    {
        if(collider1 != null && collider2 != null)
        {
            Physics.IgnoreCollision(collider1, collider2);
            Physics.IgnoreCollision(collider3, collider4);
        }
    }
}
