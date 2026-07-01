using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class RacoonHero : Subject<GameEvent>, IObserver<GameEvent>
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 10f;
    [SerializeField] private float currentHealth;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 3f;
    [SerializeField] private float runSpeed = 6f;
    [SerializeField] private float rotationAngle = 30f;
    [SerializeField] private float rotationSpeed = 6f;

    [Header("Attack")]
    [SerializeField] private GameObject fireball;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float attackCooldown = 0.8f;
    [SerializeField] private float fireballDelay = 0.25f;

    [Header("Death")]
    [SerializeField] private GameObject racoonModel;
    [SerializeField] private ParticleSystem deathEffect;
    [SerializeField] private float deathDelay = 0.8f;

    [Header("Sauron")]
    [SerializeField] private Transform eyePivot;
    [SerializeField] private Animator eyeAnimator;
    [SerializeField] private Transform sauronTarget;
    [SerializeField] private Renderer searchConeRenderer;

    [Header("Ring Power")]
    [SerializeField] private GameObject ringEffect;
    [SerializeField] private EvilRat evilRat;
    [SerializeField] private float ringDuration = 4f;
    [SerializeField] private float ringEffectTime = 4f;
    [SerializeField] private float ringCooldown = 10f;

    private Animator playerAnim;
    private CharacterController playerCtrl;
    private Vector2 moveInput;

    private bool isRunning;
    private bool isDead;
    private bool ringActive;
    private bool invencibility;

    [HideInInspector] public Vector3 motionVector;
    [HideInInspector] public float currentSpeed;

    private float motionMagnitude;
    private float dampTime = 0.2f;
    private float invencibilityTime = 1f;
    private float invencibilityStep;
    private float nextAttackTime;
    private float nextRingTime;

    private Quaternion initialRacoonRotation;

    private void Start()
    {
        playerAnim = GetComponentInChildren<Animator>();
        playerCtrl = GetComponent<CharacterController>();
        currentHealth = maxHealth;

        if (playerAnim != null)
        {
            initialRacoonRotation = playerAnim.transform.localRotation;

            if (racoonModel == null)
                racoonModel = playerAnim.gameObject;
        }

        if (sauronTarget == null && Camera.main != null)
            sauronTarget = Camera.main.transform;

        if (deathEffect != null)
            deathEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        if (ringEffect != null)
            ringEffect.SetActive(false);
    }

    private void FixedUpdate()
    {
        if (!isDead)
            Motion();

        if (invencibility && !isDead)
            Invencibility();
    }

    private void LateUpdate()
    {
        if (!isDead)
            RotateRacoon();
    }

    public void Move(InputAction.CallbackContext ctx)
    {
        if (!isDead)
            moveInput = ctx.ReadValue<Vector2>();
    }

    public void Run(InputAction.CallbackContext ctx)
    {
        if (!isDead)
            isRunning = ctx.ReadValueAsButton();
    }

    private void Motion()
    {
        motionVector = new Vector3(moveInput.x, 0f, moveInput.y);
        motionMagnitude = moveInput.magnitude;
        currentSpeed = walkSpeed;

        float animationSpeed = moveInput.y;

        if (motionMagnitude > 0f && isRunning)
        {
            currentSpeed = runSpeed;
            animationSpeed *= 2.5f;
        }

        if (motionVector.magnitude > 1f)
            motionVector.Normalize();

        if (playerCtrl != null && playerCtrl.enabled)
            playerCtrl.Move(motionVector * currentSpeed * Time.fixedDeltaTime);

        if (playerAnim == null)
            return;

        playerAnim.SetFloat("Turn", moveInput.x, dampTime, Time.fixedDeltaTime);
        playerAnim.SetFloat("Speed", animationSpeed, dampTime, Time.fixedDeltaTime);
        playerAnim.SetLayerWeight(1, (currentHealth / maxHealth) <= 0.25f ? 0.7f : 0f);
    }

    public void TakeDamage(float damage)
    {
        if (isDead || invencibility || ringActive)
            return;

        if (playerCtrl == null || !playerCtrl.enabled)
            return;

        currentHealth = Mathf.Clamp(currentHealth - damage, 0f, maxHealth);

        if (playerAnim != null)
            playerAnim.SetTrigger("Damage");

        Notify(GameEvent.PlayerDamage, new float[] { currentHealth, maxHealth });

        if (currentHealth <= 0f)
        {
            StartCoroutine(DeathSequence());
            return;
        }

        invencibility = true;
        invencibilityStep = 0f;
    }

    private IEnumerator DeathSequence()
    {
        isDead = true;
        invencibility = false;
        moveInput = Vector2.zero;
        motionVector = Vector3.zero;
        isRunning = false;

        CancelInvoke(nameof(SpawnFireball));

        if (playerAnim != null)
        {
            playerAnim.SetFloat("Speed", 0f);
            playerAnim.SetFloat("Turn", 0f);
        }

        if (playerCtrl != null)
            playerCtrl.enabled = false;

        AimSauronAtTarget();

        if (deathEffect != null)
            deathEffect.Play();

        yield return new WaitForSeconds(deathDelay);

        if (racoonModel != null)
            racoonModel.SetActive(false);

        Notify(GameEvent.GameEnd, false);
    }

    private void AimSauronAtTarget()
    {
        if (eyePivot == null)
            return;

        if (eyeAnimator != null)
            eyeAnimator.enabled = false;

        if (sauronTarget == null && Camera.main != null)
            sauronTarget = Camera.main.transform;

        if (sauronTarget == null)
            return;

        Vector3 targetPosition = sauronTarget.position;
        eyePivot.LookAt(targetPosition, Vector3.up);

        if (searchConeRenderer == null)
            return;

        Material coneMaterial = searchConeRenderer.material;
        Color brightRed = new Color(6f, 0.3f, 0f, 1f);

        coneMaterial.SetFloat("_Transparency", 1f);
        coneMaterial.SetColor("_Color1", brightRed);
        coneMaterial.SetColor("_Color2", brightRed);
    }

    private void Invencibility()
    {
        invencibilityStep += Time.deltaTime;

        if (invencibilityStep >= invencibilityTime)
        {
            invencibilityStep = 0f;
            invencibility = false;
        }
    }

    private void RotateRacoon()
    {
        if (playerAnim == null)
            return;

        float angle = moveInput.x * rotationAngle;
        Quaternion targetRotation = initialRacoonRotation * Quaternion.AngleAxis(angle, Vector3.up);

        playerAnim.transform.localRotation = Quaternion.Slerp( playerAnim.transform.localRotation,
                                                               targetRotation, rotationSpeed * Time.deltaTime );
    }

    public void Attack(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed || isDead || Time.time < nextAttackTime)
            return;

        nextAttackTime = Time.time + attackCooldown;

        if (playerAnim != null)
            playerAnim.SetTrigger("Attack");

        Invoke(nameof(SpawnFireball), fireballDelay);
    }

    private void SpawnFireball()
    {
        if (isDead || fireball == null || firePoint == null)
            return;

        Vector3 fireDirection = transform.forward;

        GameObject fireballInstance = Instantiate( fireball, firePoint.position,             
                                                   Quaternion.LookRotation(fireDirection) );

        Fireball fireballScript = fireballInstance.GetComponent<Fireball>();

        if (fireballScript != null) fireballScript.SetDirection(fireDirection);
    }

    public void UseRing(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed || isDead || ringActive || Time.time < nextRingTime)
            return;

        StartCoroutine(RingPower());
    }

    private IEnumerator RingPower()
    {
        ringActive = true;
        nextRingTime = Time.time + ringCooldown;

        if (ringEffect != null)
            ringEffect.SetActive(true);

        if (racoonModel != null)
            racoonModel.SetActive(false);

        if (evilRat != null)
            evilRat.PauseSpawning(ringDuration);

        float visibleTime = Mathf.Min(ringEffectTime, ringDuration);

        yield return new WaitForSeconds(visibleTime);

        if (ringEffect != null)
            ringEffect.SetActive(false);

        yield return new WaitForSeconds(ringDuration - visibleTime);

        if (!isDead && racoonModel != null)
            racoonModel.SetActive(true);

        ringActive = false;
    }

    public void OnNotify(GameEvent gameEvent, object data)
    {
        if (gameEvent != GameEvent.GameEnd)
            return;

        moveInput = Vector2.zero;
        motionVector = Vector3.zero;
        isRunning = false;

        CancelInvoke(nameof(SpawnFireball));

        if (playerCtrl != null)
            playerCtrl.enabled = false;

        enabled = false;
    }
}

