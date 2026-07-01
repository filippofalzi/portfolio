using System.Collections.Generic;
using UnityEngine;

public class CockroachPool : MonoBehaviour
{
    [SerializeField] private GameObject cockroachPrefab;
    [SerializeField] private int poolSize = 10;

    private Queue<GameObject> cockroachPool;

    private void Start()
    {
        InitPool();
    }

    private void InitPool()
    {
        cockroachPool = new Queue<GameObject>();

        for (int i = 0; i < poolSize; i++)
        {
            GameObject cockroachInstance =
                Instantiate(cockroachPrefab, transform);

            Cockroach cockroachScript =
                cockroachInstance.GetComponent<Cockroach>();

            cockroachScript.SetCockroachOwner(this);

            cockroachInstance.SetActive(false);
            cockroachPool.Enqueue(cockroachInstance);
        }
    }

    public GameObject SpawnCockroach(
    Vector3 position,
    Quaternion rotation)
    {
        if (cockroachPool.Count == 0)
        {
            Debug.LogWarning("No cockroaches available in the pool.");
            return null;
        }

        GameObject cockroachInstance =
            cockroachPool.Dequeue();

        cockroachInstance.transform.position =
            position;

        cockroachInstance.transform.rotation =
            rotation;

        cockroachInstance.SetActive(true);

        return cockroachInstance;
    }


    public void DespawnCockroach(
        GameObject cockroachInstance)
    {
        cockroachInstance.SetActive(false);

        cockroachInstance.transform.SetParent(transform);

        cockroachPool.Enqueue(cockroachInstance);
    }
}
