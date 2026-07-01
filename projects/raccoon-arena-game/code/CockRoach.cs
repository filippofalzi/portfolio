using System.Collections;
using UnityEngine;

public class Cockroach : MonoBehaviour
{
    [SerializeField] private float maxHealth = 3f;
    [SerializeField] private float cockroachDamage = 1f;
    [SerializeField] private float cockroachSpeed = 2f;
    [SerializeField] private float cockroachLifeTime = 20f;
    [SerializeField] private float deathTime = 0.3f;

    private CockroachPool cockroachPool;
    private Animator cockroachAnim;
    private RacoonHero player;

    private float currentHealth;
    private bool isDead;

    private void Awake()
    {
        cockroachAnim = GetComponentInChildren<Animator>();
        player = FindFirstObjectByType<RacoonHero>();
    }

    private void OnEnable()
    {
        currentHealth = maxHealth;
        isDead = false;

        if (cockroachAnim != null)
            cockroachAnim.SetFloat("Speed", 1f);

        StopAllCoroutines();
        StartCoroutine(SetInactiveDelayed());
    }

    private void FixedUpdate()
    {
        if (!isDead)
        {
            transform.position +=
                Vector3.back *
                cockroachSpeed *
                Time.fixedDeltaTime;
        }
    }

    public void SetCockroachOwner(
        CockroachPool pool)
    {
        cockroachPool = pool;
    }

    public void TakeDamage(float damage)
    {
        if (isDead)
            return;

        currentHealth -= damage;

        if (currentHealth <= 0f)
            Die();
    }

    private void Die()
    {
        isDead = true;

        StopAllCoroutines();

        if (cockroachAnim != null)
            cockroachAnim.SetFloat("Speed", 0f);

        StartCoroutine(DespawnAfterDeath());
    }

    private IEnumerator DespawnAfterDeath()
    {
        yield return new WaitForSeconds(deathTime);

        if (cockroachPool != null)
            cockroachPool.DespawnCockroach(gameObject);
    }

    private IEnumerator SetInactiveDelayed()
    {
        yield return new WaitForSeconds(
            cockroachLifeTime
        );

        if (!isDead && cockroachPool != null)
            cockroachPool.DespawnCockroach(gameObject);
    }

    private void OnTriggerEnter(Collider collision)
    {
        if (!collision.CompareTag("Trash"))
            return;

        if (player != null)
            player.TakeDamage(cockroachDamage);

        if (cockroachPool != null)
            cockroachPool.DespawnCockroach(gameObject);
    }
}