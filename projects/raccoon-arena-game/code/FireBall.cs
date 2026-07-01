using System.Collections;
using UnityEngine;

public class Fireball : MonoBehaviour
{
    private Vector3 fireballDirection;

    [SerializeField] private float fireballDamage = 2f;
    [SerializeField] private float fireballSpeed = 8f;
    [SerializeField] private float fireballLifeTime = 4f;

    private void FixedUpdate()
    {
        transform.position +=  fireballDirection * fireballSpeed * Time.fixedDeltaTime;
    }

    private void OnEnable()
    {
        StopAllCoroutines();
        StartCoroutine(SetInactiveDelayed());
    }

    public void SetDirection(Vector3 direction)
    {
        fireballDirection = direction.normalized;
    }

    private IEnumerator SetInactiveDelayed()
    {
        yield return new WaitForSeconds(fireballLifeTime);
        Destroy(gameObject);
    }

    private void OnTriggerEnter(Collider collision)
    {
        if (collision.GetComponentInParent<RacoonHero>() != null)
            return;

        Cockroach cockroach =
            collision.GetComponentInParent<Cockroach>();

        if (cockroach != null)
        {
            if (ParticlePool.Instance != null)
            {
                ParticlePool.Instance.PlayParticles(
                    cockroach.transform.position
                );
            }

            cockroach.TakeDamage(fireballDamage);
            Destroy(gameObject);
            return;
        }

        EvilRat rat =
            collision.GetComponentInParent<EvilRat>();

        if (rat != null)
        {
            rat.TakeDamage(fireballDamage);
            Destroy(gameObject);
            return;
        }

        Destroy(gameObject);
    }

}
