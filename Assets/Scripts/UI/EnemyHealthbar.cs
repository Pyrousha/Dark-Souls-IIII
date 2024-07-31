using UnityEngine;
using UnityEngine.UI;

public class EnemyHealthbar : MonoBehaviour, IPoolableObject
{
    [field: SerializeField] public RectTransform C_RectTransform;
    [SerializeField] private Slider hpBar;
    [SerializeField] private Slider tempHpBar;

    private float targValue;

    private void Awake()
    {
        C_RectTransform = GetComponent<RectTransform>();
    }

    public void ResetData()
    {
        hpBar.value = 1;
        tempHpBar.value = 1;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="_tempHealthPercent"> Percentage of max health that is temporary health </param>
    /// <param name="_healthPercent"> Percentage of health remaining out of max health </param>
    public void UpdateUI(float _tempHealthPercent, float _healthPercent)
    {
        //Temp hp is rendered behind
        tempHpBar.value = _healthPercent;

        hpBar.value = _healthPercent - _tempHealthPercent;
    }

    void IPoolableObject.ResetData()
    {
        throw new System.NotImplementedException();
    }
}
