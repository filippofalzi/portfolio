using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Fixer : MonoBehaviour {

    public bool IsInside (Vector3 pos)
    {
        return GetComponent<Collider>().bounds.Contains(pos);
    }
}
