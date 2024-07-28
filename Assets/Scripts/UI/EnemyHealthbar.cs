using UnityEngine;
using UnityEngine.UI;

public class EnemyHealthbar : PoolableObject
{
    [field: SerializeField] public RectTransform C_RectTransform;
    [SerializeField] private Slider hpBar;
    [SerializeField] private Slider tempHpBar;

    private void Awake()
    {
        C_RectTransform = GetComponent<RectTransform>();
    }

    public override void ResetData()
    {
        hpBar.value = 1;
        tempHpBar.value = 1;
    }

    public void TakeDamage(float _percent)
    {
        hpBar.value = Mathf.Max(0, hpBar.value - _percent);
        tempHpBar.value = Mathf.Max(0, tempHpBar.value - _percent);
    }

    public void TakeTempDamage(float _percent)
    {
        hpBar.value = Mathf.Max(0, hpBar.value - _percent);
    }
}
