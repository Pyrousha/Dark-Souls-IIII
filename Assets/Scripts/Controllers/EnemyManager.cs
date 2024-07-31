using System.Collections.Generic;
using UnityEngine;

public class EnemyManager : Singleton<EnemyManager>
{
    [field: SerializeField] public LayerMask HurtboxLayer { get; private set; }

    public List<Enemy> AliveEnemies { get; private set; } = new List<Enemy>();

    public void AddToEnemyList(Enemy _enemy)
    {
        if (!AliveEnemies.Contains(_enemy))
            AliveEnemies.Add(_enemy);
    }

    public void RemoveFromEnemyList(Enemy _enemy)
    {
        AliveEnemies.Remove(_enemy);
    }

    public Enemy GetClosestEnemy(Vector3 _playerPos)
    {
        float smallestDist = 100000;
        Enemy enemy = null;

        foreach (Enemy currEnemy in AliveEnemies)
        {
            Vector3 dist = currEnemy.transform.position - _playerPos;
            if (dist.sqrMagnitude < smallestDist)
            {
                smallestDist = dist.sqrMagnitude;
                enemy = currEnemy;
            }
        }

        return enemy;
    }
}
