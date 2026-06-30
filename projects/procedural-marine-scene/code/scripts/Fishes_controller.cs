using UnityEngine;
using System.Collections.Generic;

public class FishFlockController : MonoBehaviour
{
    public struct FishData
    {
        public Vector4 position_phase;   // xyz = position, w = tail phase
        public Vector4 velocity_orbit;   // xyz = velocity, w = orbit offset
        public Vector4 extra;            // x = height offset
    }

    const int FishDataDim = 48; // 3 Vector4 = 3 * 16 bytes

    [Header("References")]
    public ComputeShader movementCompute;
    public Material fishMaterial;
    public Material seaMaterial;

    [Header("Fish")]
    public int fishCount = 64;
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

    [Header("Fish-Waves (Optional)")]
    public bool followSeaWaves = true;
    public float fallbackWaveHeight = 0.25f;
    public float fallbackWaveSpeed = 1.0f;
    public float fallbackWaveScale = 2.0f;

    ComputeBuffer fishBuffer;
    int kernel;

    static readonly HashSet<FishFlockController> ActiveControllers = new();

    void OnEnable()
    {
        ActiveControllers.Add(this);
    }

    void OnDisable()
    {
        ActiveControllers.Remove(this);
    }

    public static void GetActiveControllers(List<FishFlockController> dst)
    {
        dst.Clear();
        foreach (var c in ActiveControllers)
            if (c != null && c.isActiveAndEnabled)
                dst.Add(c);
    }

    public bool TryGetDrawData(out Material material, out int count, out ComputeBuffer buffer)
    {
        material = fishMaterial;
        count = fishCount;
        buffer = fishBuffer;
        return material != null && buffer != null && count > 0;
    }

    void Start()
    {
        kernel = movementCompute.FindKernel("CSMain");

        fishBuffer = new ComputeBuffer(
            fishCount,
            FishDataDim
        );

        FishData[] fishArray = new FishData[fishCount];

        for (int i = 0; i < fishCount; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float radius = Random.Range(0.5f, spawnRadius);

            Vector3 pos = flockCenter + new Vector3(
                Mathf.Cos(angle) * radius,
                Random.Range(-0.2f, 0.2f),
                Mathf.Sin(angle) * radius
            );

            Vector3 vel = new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle)).normalized;

            fishArray[i].position_phase = new Vector4(pos.x, pos.y, pos.z, Random.Range(0f, Mathf.PI * 2f));
            fishArray[i].velocity_orbit = new Vector4(vel.x, vel.y, vel.z, Random.Range(0f, Mathf.PI * 2f));
            fishArray[i].extra = new Vector4(pos.y, 0f, 0f, 0f);
        }

        fishBuffer.SetData(fishArray);

        movementCompute.SetBuffer(kernel, "_FishBuffer", fishBuffer);
        fishMaterial.SetBuffer("_FishBuffer", fishBuffer);

        fishMaterial.SetInt("_FishCount", fishCount);
    }

    void Update()
    {
        if (fishBuffer == null) return;

        movementCompute.SetInt("_FishCount", fishCount);
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

        float tailPhaseSpeed = 3.5f;
        if (fishMaterial != null && fishMaterial.HasProperty("_TailSpeed"))
            tailPhaseSpeed = fishMaterial.GetFloat("_TailSpeed");
        movementCompute.SetFloat("_TailPhaseSpeed", tailPhaseSpeed);

        if (followSeaWaves)
        {
            float waveHeight = fallbackWaveHeight;
            float waveSpeed = fallbackWaveSpeed;
            float waveScale = fallbackWaveScale;

            if (seaMaterial != null)
            {
                if (seaMaterial.HasProperty("_WaveHeight")) waveHeight = seaMaterial.GetFloat("_WaveHeight");
                if (seaMaterial.HasProperty("_WaveSpeed")) waveSpeed = seaMaterial.GetFloat("_WaveSpeed");
                if (seaMaterial.HasProperty("_WaveScale")) waveScale = seaMaterial.GetFloat("_WaveScale");
            }

            movementCompute.SetFloat("_WaveHeight", waveHeight);
            movementCompute.SetFloat("_WaveSpeed", waveSpeed);
            movementCompute.SetFloat("_WaveScale", waveScale);
        }
        else
        {
            movementCompute.SetFloat("_WaveHeight", 0.0f);
            movementCompute.SetFloat("_WaveSpeed", 0.0f);
            movementCompute.SetFloat("_WaveScale", 1.0f);
        }

        int groups = Mathf.CeilToInt(fishCount / 64.0f);
        movementCompute.Dispatch(kernel, groups, 1, 1);
    }

    void OnDestroy()
    {
        if (fishBuffer != null)
        {
            fishBuffer.Release();
            fishBuffer = null;
        }
    }
}