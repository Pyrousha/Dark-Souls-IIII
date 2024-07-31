using UnityEngine;

public class Entity : MonoBehaviour
{
    public enum EntityTeam
    {
        Player,
        Enemy
    }

    [field: Header("Entity Values")]
    [field: SerializeField] public EntityTeam Team { get; protected set; }

    public bool IsHpMax => (MaxHp == currHp);
    [SerializeField] private bool startAtMaxHp = true;
    [SerializeField] private int MaxHp;
    private int currHp;
    private int currTempHp;

    public bool IsDead { get; private set; } = false;

    [field: SerializeField] public EnemyHealthbar EnemyHealthbar { get; protected set; }
    [SerializeField] protected PlayerHealthbar playerHealthbar;
    private bool isEnemy;

    private Enemy c_Enemy;

    protected void Awake()
    {
        c_Enemy = GetComponent<Enemy>();
    }

    protected void Start()
    {
        if (startAtMaxHp)
            currHp = MaxHp;

        isEnemy = (EnemyHealthbar != null);

        if (isEnemy)
            EnemyHealthbar.UpdateUI((float)currTempHp / MaxHp, (float)currHp / MaxHp);
        else
            playerHealthbar.UpdateUI((float)currHp / MaxHp);
    }

    public void TakeTempDamage(int _damageToTake)
    {
        if (!isEnemy)
            return;

        currTempHp = Mathf.Min(currHp, currTempHp + _damageToTake);
        EnemyHealthbar.UpdateUI((float)currTempHp / MaxHp, (float)currHp / MaxHp);
    }

    public void TakeDamage(int _damageToTake)
    {
        if (IsDead)
            return;

        //Take Damage
        currHp = Mathf.Max(0, currHp - _damageToTake);
        if (currHp == 0)
            IsDead = true;

        #region Update Healthbars
        if (isEnemy)
        {
            int tempHpToLose = Mathf.Max(0, currTempHp - _damageToTake);
            currTempHp -= tempHpToLose;

            //Consume temp health
            currHp = Mathf.Max(0, currHp - _damageToTake);
            if (currHp == 0)
                IsDead = true;

            EnemyHealthbar.UpdateUI((float)currTempHp / MaxHp, (float)currHp / MaxHp);
        }
        else
        {
            playerHealthbar.UpdateUI((float)currHp / MaxHp);
        }
        #endregion

        if (IsDead)
        {
            //Do die logic
            if (c_Enemy != null)
            {
                EnemyManager.Instance.RemoveFromEnemyList(c_Enemy);
                ObjectPooler.Instance.AddToPool(c_Enemy);
                ObjectPooler.Instance.AddToPool(EnemyHealthbar);
            }
        }
    }

    protected void FullHeal()
    {
        currHp = MaxHp;
        IsDead = false;

        if (isEnemy)
        {
            EnemyHealthbar.UpdateUI((float)currTempHp / MaxHp, (float)currHp / MaxHp);
            EnemyManager.Instance.AddToEnemyList(c_Enemy);
        }
        else
            playerHealthbar.UpdateUI((float)currHp / MaxHp);
    }
}