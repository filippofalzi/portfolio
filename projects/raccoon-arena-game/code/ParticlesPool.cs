using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ParticlePool : MonoBehaviour
{
    public static ParticlePool Instance;

    [SerializeField] private ParticleSystem particlePrefab;
    [SerializeField] private int poolSize = 10;

    private Queue<ParticleSystem> particles =
        new Queue<ParticleSystem>();

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        for (int i = 0; i < poolSize; i++)
        {
            ParticleSystem particle =
                Instantiate(particlePrefab, transform);

            particle.gameObject.SetActive(false);
            particles.Enqueue(particle);
        }
    }

    public void PlayParticles(Vector3 position)
    {
        if (particles.Count == 0)
            return;

        ParticleSystem particle = particles.Dequeue();

        particle.transform.position = position;
        particle.gameObject.SetActive(true);

        particle.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear
        );

        particle.Play();

        StartCoroutine(ReturnToPool(particle));
    }

    private IEnumerator ReturnToPool(ParticleSystem particle)
    {
        yield return new WaitUntil(
            () => !particle.IsAlive(true)
        );

        particle.gameObject.SetActive(false);
        particles.Enqueue(particle);
    }
}