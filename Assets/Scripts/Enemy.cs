using UnityEngine;

public class Enemy : Entity, IPoolableObject
{
    [field: SerializeField] public Transform LockonPivot { get; private set; }
    [field: SerializeField] public Transform HPBarPivot { get; private set; }

    public void ResetData()
    {
        base.FullHeal();
    }

    public new void Start()
    {
        if (playerHealthbar == null)
        {
            EnemyManager.Instance.AddToEnemyList(this);
            EnemyHealthbar = ObjectPooler.Instance.GetFromPool<EnemyHealthbar>();
        }

        base.Start();
    }
}
