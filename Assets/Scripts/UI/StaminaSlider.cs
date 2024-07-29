using UnityEngine;
using UnityEngine.UI;

public class StaminaSlider : MonoBehaviour
{
    public RectTransform C_RectTransform { get; private set; }
    [field: SerializeField] public Slider Slider { get; private set; }

    private void Awake()
    {
        C_RectTransform = GetComponent<RectTransform>();
    }
}
