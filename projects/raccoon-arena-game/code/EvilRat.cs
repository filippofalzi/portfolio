using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EvilRat : Subject<GameEvent>
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 20f;
    [SerializeField] private float currentHealth;

    [Header("Movement")]
    [SerializeField] private float movementSpeed = 3f;
    [SerializeField] private float leftLimit = -7f;
    [SerializeField] private float rightLimit = 7f;
    [SerializeField] private float directionChangeTime = 1.5f;
    [SerializeField] private float stopTime = 1f;
    [SerializeField] private float rotationAngle = 30f;
    [SerializeField] private float rotationSpeed = 6f;

    [Header("Cockroach Spawn")]
    [SerializeField] private CockroachPool cockroachPool;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private float spawnInterval = 5f;

    [Header("Animations")]
    [SerializeField] private Animator ratAnimator;

    private float direction = 1f;
    private float directionTimer;

    private bool isStopped;
    private bool isDead;
    private bool spawnPaused;

    private Quaternion initialRatRotation;

    private void Start()
    {
        currentHealth = maxHealth;

        if (ratAnimator == null)
            ratAnimator = GetComponentInChildren<Animator>();

        if (ratAnimator != null)
            initialRatRotation = ratAnimator.transform.localRotation;

        StartCoroutine(SpawnCockroaches());
    }

    private void Update()
    {
        if (isDead || isStopped)
            return;

        MoveRat();
        ChangeDirectionRandomly();
    }

    private void LateUpdate()
    {
        RotateRat();
    }

    private void MoveRat()
    {
        if (Time.timeSinceLevelLoad < 3f)
            return;

        transform.position +=
            Vector3.right *
            direction *
            movementSpeed *
            Time.deltaTime;

        if (transform.position.x >= rightLimit)
            direction = -1f;

        if (transform.position.x <= leftLimit)
            direction = 1f;

        if (ratAnimator != null)
            ratAnimator.SetFloat("SideWalk", direction);
    }

    private void RotateRat()
    {
        if (ratAnimator == null)
            return;

        float angle =
            (!isStopped && !isDead)
            ? direction * -rotationAngle
            : 0f;

        Quaternion targetRotation =
            initialRatRotation *
            Quaternion.AngleAxis(angle, Vector3.up);

        ratAnimator.transform.localRotation =
            Quaternion.Slerp(
                ratAnimator.transform.localRotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );
    }

    private void ChangeDirectionRandomly()
    {
        directionTimer += Time.deltaTime;

        if (directionTimer < directionChangeTime)
            return;

        directionTimer = 0f;

        int randomAction = Random.Range(0, 3);

        if (randomAction == 0)
            direction *= -1f;
        else if (randomAction == 1)
            StartCoroutine(StopRat());
    }

    private IEnumerator StopRat()
    {
        isStopped = true;

        if (ratAnimator != null)
            ratAnimator.SetFloat("SideWalk", 0f);

        yield return new WaitForSeconds(stopTime);

        direction =
            Random.value > 0.5f
            ? 1f
            : -1f;

        isStopped = false;
    }

    private IEnumerator SpawnCockroaches()
    {
        yield return new WaitForSeconds(8f);

        while (!isDead)
        {
            if (spawnPaused)
            {
                yield return null;
                continue;
            }

            SpawnCockroach();

            yield return new WaitForSeconds(spawnInterval);
        }
    }

    private void SpawnCockroach()
    {
        if (cockroachPool == null || spawnPoint == null)
            return;

        cockroachPool.SpawnCockroach(
            spawnPoint.position,
            spawnPoint.rotation
        );
    }

    public void TakeDamage(float damage)
    {
        if (isDead)
            return;

        currentHealth =
            Mathf.Clamp(
                currentHealth - damage,
                0f,
                maxHealth
            );

        if (currentHealth <= 0f)
        {
            Die();
        }
        else if (ratAnimator != null)
        {
            ratAnimator.SetTrigger("Damage");
        }
    }

    private void Die()
    {
        isDead = true;
        StopAllCoroutines();

        if (ratAnimator != null)
        {
            ratAnimator.SetFloat("SideWalk", 0f);
            ratAnimator.SetTrigger("Death");
        }

        Notify(GameEvent.GameEnd, true);
    }

    public void PauseSpawning(float duration)
    {
        StartCoroutine(
            PauseSpawningCoroutine(duration)
        );
    }

    private IEnumerator PauseSpawningCoroutine(
        float duration)
    {
        spawnPaused = true;

        yield return new WaitForSeconds(duration);

        spawnPaused = false;
    }
}

