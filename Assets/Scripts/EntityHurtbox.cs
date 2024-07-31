using UnityEngine;

public class EntityHurtbox : MonoBehaviour
{
    [field: SerializeField] public Entity AttachedEntity { get; private set; }
}