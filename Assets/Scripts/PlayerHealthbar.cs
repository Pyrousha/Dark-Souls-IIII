using UnityEngine;
using UnityEngine.UI;

public class PlayerHealthbar : MonoBehaviour
{
    [SerializeField] private Slider hpBar;

    public void UpdateUI(float _percent)
    {
        hpBar.value = _percent;
    }
}