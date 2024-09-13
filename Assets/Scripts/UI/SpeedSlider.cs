using UnityEngine;
using UnityEngine.UI;

public class SpeedSlider : MonoBehaviour
{
    [SerializeField] private Color[] colors;
    [SerializeField] private Slider slider;
    [SerializeField] private Image sliderFill;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="_speedState">Value between 0 and 3</param>
    public void SetSliderVisualState(float _speedState)
    {
        _speedState = Mathf.Clamp(_speedState, 0f, 3f);
        slider.value = _speedState / 3f;

        sliderFill.color = colors[Mathf.FloorToInt(_speedState)];
    }

    public void SetSliderVisualState_Percent(float _percent)
    {
        _percent = Mathf.Clamp(_percent, 0f, 1f);
        slider.value = _percent;

        sliderFill.color = colors[Mathf.FloorToInt(_percent * 3)];
    }
}
