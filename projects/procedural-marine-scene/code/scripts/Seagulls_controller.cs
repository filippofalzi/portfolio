using UnityEngine;
using System.Collections.Generic;

public class SeagullsController : MonoBehaviour
{
    public struct BirdData
    {
        public Vector4 position_phase;   // xyz = position, w = flap phase
        public Vector4 velocity_orbit;   // xyz = velocity, w = orbit offset
        public Vector4 extra;            // x = height offset
    }

    const int BirdDataDim = 48; // 3 Vector4 = 3 * 16 bytes

    [Header("References")]
    public ComputeShader movementCompute;
    public Material SeagullsMaterial;

    [Header("Birds")]
    public int birdCount = 64;
    public float spawnRadius = 3.0f;
    public Vector3 flockCenter = Vector3.zero;

    [Header("Orbit")]
    public float orbitRadiusMin = 1.0f;
    public float orbitRadiusMax = 3.0f;
    public float orbitSpeedMin = 0.6f;
    public float orbitSpeedMax = 1.2f;

    [Header("Boids Light")]
    public float neighborRadius = 1.5f;
    public float separationRadius = 0.5f;

    public float pathWeight = 1.0f;
    public float alignmentWeight = 0.2f;
    public float cohesionWeight = 0.15f;
    public float separationWeight = 0.8f;

    public float maxSpeed = 2.0f;

    ComputeBuffer birdBuffer;
    int kernel;

    static readonly HashSet<SeagullsController> ActiveControllers = new();

    void OnEnable()
    {
        ActiveControllers.Add(this);
    }

    void OnDisable()
    {
        ActiveControllers.Remove(this);
    }

    public static void GetActiveControllers(List<SeagullsController> dst)
    {
        dst.Clear();
        foreach (var c in ActiveControllers)
            if (c != null && c.isActiveAndEnabled)
                dst.Add(c);
    }

    public bool TryGetDrawData(out Material material, out int count, out ComputeBuffer buffer)
    {
        material = SeagullsMaterial;
        count = birdCount;
        buffer = birdBuffer;
        return material != null && buffer != null && count > 0;
    }

    void Start()
    {
        kernel = movementCompute.FindKernel("CSMain");

        birdBuffer = new ComputeBuffer(
            birdCount,
            BirdDataDim
        );

        BirdData[] birds = new BirdData[birdCount];

        for (int i = 0; i < birdCount; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float radius = Random.Range(0.5f, spawnRadius);

            Vector3 pos = flockCenter + new Vector3(
                Mathf.Cos(angle) * radius,
                Random.Range(-0.2f, 0.2f),
                Mathf.Sin(angle) * radius
            );

            Vector3 vel = new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle)).normalized;

            birds[i].position_phase = new Vector4(pos.x, pos.y, pos.z, Random.Range(0f, Mathf.PI * 2f));
            birds[i].velocity_orbit = new Vector4(vel.x, vel.y, vel.z, Random.Range(0f, Mathf.PI * 2f));
            birds[i].extra = new Vector4(pos.y, 0f, 0f, 0f);
        }

        birdBuffer.SetData(birds);

        movementCompute.SetBuffer(kernel, "_BirdBuffer", birdBuffer);
        SeagullsMaterial.SetBuffer("_BirdBuffer", birdBuffer);

        SeagullsMaterial.SetInt("_BirdCount", birdCount);
    }

    void Update()
    {
        if (birdBuffer == null) return;

        movementCompute.SetInt("_BirdCount", birdCount);
        movementCompute.SetVector("_FlockCenter", flockCenter);

        movementCompute.SetFloat("_OrbitRadiusMin", orbitRadiusMin);
        movementCompute.SetFloat("_OrbitRadiusMax", orbitRadiusMax);
        movementCompute.SetFloat("_OrbitSpeedMin", orbitSpeedMin);
        movementCompute.SetFloat("_OrbitSpeedMax", orbitSpeedMax);

        movementCompute.SetFloat("_NeighborRadius", neighborRadius);
        movementCompute.SetFloat("_SeparationRadius", separationRadius);

        movementCompute.SetFloat("_PathWeight", pathWeight);
        movementCompute.SetFloat("_AlignmentWeight", alignmentWeight);
        movementCompute.SetFloat("_CohesionWeight", cohesionWeight);
        movementCompute.SetFloat("_SeparationWeight", separationWeight);

        movementCompute.SetFloat("_MaxSpeed", maxSpeed);
        movementCompute.SetFloat("_DeltaTime", Time.deltaTime);
        movementCompute.SetFloat("_TimeValue", Time.time);

        int groups = Mathf.CeilToInt(birdCount / 64.0f);
        movementCompute.Dispatch(kernel, groups, 1, 1);
    }

    void OnDestroy()
    {
        if (birdBuffer != null)
        {
            birdBuffer.Release();
            birdBuffer = null;
        }
    }
}