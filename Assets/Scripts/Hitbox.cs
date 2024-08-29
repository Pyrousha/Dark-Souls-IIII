using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Entity;

public class Hitbox : MonoBehaviour
{
    [SerializeField] private bool showMeshRend;
    [SerializeField] private bool enableOnStart = false;
    [Space(10)]
    [SerializeField] private int damage;
    [SerializeField] private bool dealsTemporaryHealth;
    [SerializeField] private EntityTeam[] validTargets;

    private Collider c_collider;
    private MeshRenderer c_meshRenderer;

    private List<Entity> hitEntities = new List<Entity>();

    private int currDamage;
    private bool hitboxActive = false;

    private float t_disableTime;

    private void Awake()
    {
        c_collider = GetComponent<Collider>();
        c_meshRenderer = GetComponent<MeshRenderer>();

        if (c_collider.enabled)
        {
            Debug.LogWarning($"Collider was enabled on hitbox \"{gameObject.name}\", disabling.");
            c_collider.enabled = false;
            if (c_meshRenderer != null)
                c_meshRenderer.enabled = false;
        }

        if (enableOnStart)
            EnableHitbox(99999999999);
    }

    private void Update()
    {
        if (hitboxActive && Time.time >= t_disableTime)
            DisableHitbox();
    }

    public void EnableHitbox(float duration, int _damageOverride = 0)
    {
        if (hitboxActive)
        {
            Debug.LogWarning($"hitbox \"{gameObject.name}\" is already active, skipping call to enable.");
            return;
        }

        hitboxActive = true;
        t_disableTime = Time.time + duration;

        if (_damageOverride > 0)
            currDamage = _damageOverride;
        else
            currDamage = damage;

        c_collider.enabled = true;
        if (c_meshRenderer != null && showMeshRend)
            c_meshRenderer.enabled = true;
    }

    public void DisableHitbox()
    {
        hitboxActive = false;
        c_collider.enabled = false;
        if (c_meshRenderer != null && showMeshRend)
            c_meshRenderer.enabled = false;

        hitEntities.Clear();
    }

    private void OnTriggerEnter(Collider other)
    {
        Entity entity = other.GetComponent<EntityHurtbox>().AttachedEntity;
        if (validTargets.Contains(entity.Team))
        {
            if (hitEntities.Contains(entity))
                return;
            hitEntities.Add(entity);

            if (dealsTemporaryHealth)
                entity.TakeTempDamage(currDamage);
            else
                entity.TakeDamage(currDamage);
        }
    }

    private void OnValidate()
    {
        if (c_collider == null)
            c_collider = GetComponent<Collider>();
    }
}
