using UnityEngine;
using UnityEngine.AI;

public class Enemy : Entity, IPoolableObject
{
    [Header("Enemy")]
    [SerializeField] private NavMeshAgent agent;
    [field: SerializeField] public Transform LockonPivot { get; private set; }
    [field: SerializeField] public Transform HPBarPivot { get; private set; }

    [SerializeField] private Transform target;
    [SerializeField] private Hitbox hitbox;

    [SerializeField] private float attackRange;
    [SerializeField] private float scootDistance;
    [SerializeField] private float scootAccel;
    [SerializeField] private float scootSpeed;

    private float navAccel;
    private float navSpeed;

    private bool isAttacking;
    private bool isAttackStartup;

    public void ResetData()
    {
        base.FullHeal();
    }

    public new void Start()
    {
        navAccel = agent.acceleration;
        navSpeed = agent.speed;

        if (playerHealthbar == null)
        {
            EnemyManager.Instance.OnEnemySpawned(this);
            EnemyHealthbar = ObjectPooler.Instance.GetFromPool<EnemyHealthbar>();
        }

        base.Start();
    }

    private void Update()
    {
        if (IsDead)
            return;

        Vector3 toTarg = new Vector3(target.position.x - transform.position.x, 0, target.position.z - transform.position.z);

        if (!isAttacking)
        {
            agent.destination = target.position;

            if (toTarg.magnitude <= attackRange && (Vector3.Dot(transform.forward, toTarg) > 0))
            {
                //Start Attack
                isAttacking = true;
                isAttackStartup = true;
                agent.destination = transform.position;

                c_modelAnimator.SetTrigger("Attack");
            }
            else
            {
                c_modelAnimator.SetBool("Moving", true);
            }
        }
        else
        {
            if (isAttackStartup)
                transform.forward = toTarg;
        }
    }

    public void DoScoot()
    {
        agent.acceleration = scootAccel;
        agent.speed = scootSpeed;
        agent.destination = transform.position + (target.position - transform.position).normalized * scootDistance;
        isAttackStartup = false;
    }

    public void StartHitbox()
    {
        hitbox.EnableHitbox(9999);
    }

    public void EndHitbox()
    {
        hitbox.DisableHitbox();
    }

    public void OnAttackEnd()
    {
        isAttacking = false;
        agent.acceleration = navAccel;
        agent.speed = navSpeed;
    }

    protected override void OnDie()
    {
        //In case player kills enemy mid-attack
        hitbox.DisableHitbox();
    }
}
