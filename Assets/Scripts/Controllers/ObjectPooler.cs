using AYellowpaper.SerializedCollections;
using System;
using System.Collections.Generic;
using UnityEngine;

public class ObjectPooler : Singleton<ObjectPooler>
{
    public enum ObjectTypeEnum
    {
        EnemyHealthbar,
        BasicEnemy
    }

    [Serializable]
    public struct SpawnableStruct
    {
        public GameObject Prefab;
        public Transform Parent;
    }

    [SerializeField] private SerializedDictionary<ObjectTypeEnum, SpawnableStruct> prefabs;
    private Dictionary<Type, List<IPoolableObject>> objects = new Dictionary<Type, List<IPoolableObject>>();
    private Dictionary<Type, ObjectTypeEnum> typeToEnumMap = new Dictionary<Type, ObjectTypeEnum>();
    private List<Type> validTypes = new List<Type>()
    {
        typeof(EnemyHealthbar),
        typeof(Enemy)
    };

    private void Start()
    {
        typeToEnumMap.Add(typeof(EnemyHealthbar), ObjectTypeEnum.EnemyHealthbar);
        typeToEnumMap.Add(typeof(Enemy), ObjectTypeEnum.BasicEnemy);

        foreach (Type type in validTypes)
        {
            if (!objects.ContainsKey(type))
                objects.Add(type, new List<IPoolableObject>());

            if (!typeToEnumMap.ContainsKey(type))
                Debug.LogError($"typeToEnumMap is missing mapping for \"{type}\"");
        }
    }

    /// <summary>
    /// Gets an object from the corresponding pool, and enables it.\n
    /// Or if an object doesn't exist in the pool, it'll be instantiated
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public T GetFromPool<T>() where T : MonoBehaviour, IPoolableObject
    {
        Type type = typeof(T);
        ObjectTypeEnum typeToUse;

        if (validTypes.Contains(type))
        {
            typeToUse = typeToEnumMap[type];
        }
        else
        {
            Debug.LogError($"No pool of type \"{type}\" to get from");
            return null;
        }

        List<IPoolableObject> listToUse = objects[type];
        if (listToUse.Count > 0)
        {
            T objToReturn = (T)listToUse[^1];
            listToUse.RemoveAt(listToUse.Count - 1);
            objToReturn.ResetData();
            objToReturn.gameObject.SetActive(true);
            return objToReturn;
        }

        return Instantiate(prefabs[typeToUse].Prefab, prefabs[typeToUse].Parent).GetComponent<T>();
    }

    /// <summary>
    /// Adds an object to the corresponding pool, and then disable it
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="_objToAdd"></param>
    public void AddToPool<T>(T _objToAdd) where T : MonoBehaviour, IPoolableObject
    {
        Type type = typeof(T);

        if (!validTypes.Contains(type))
        {
            Debug.LogError($"No pool of type \"{type}\" to add to");
            return;
        }

        List<IPoolableObject> listToUse = objects[type];

        listToUse.Add(_objToAdd);
        _objToAdd.gameObject.SetActive(false);
    }
}
