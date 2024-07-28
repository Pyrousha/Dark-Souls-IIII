using UnityEngine;

public class Enemy : MonoBehaviour
{
    [field: SerializeField] public Transform LockonPivot { get; private set; }
    [field: SerializeField] public Transform HPBarPivot { get; private set; }

    [SerializeField] private int maxHp;
    private int currHp;

    public EnemyHealthbar Healthbar { get; private set; }

    private void Start()
    {
        EnemyManager.Instance.AddToEnemyList(this);
        Healthbar = ObjectPooler.Instance.GetFromPool<EnemyHealthbar>();

        currHp = maxHp;
    }

    public bool IsHpMax => (maxHp == currHp);
}
