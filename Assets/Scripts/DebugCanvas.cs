using TMPro;
using UnityEngine;

public class DebugCanvas : Singleton<DebugCanvas>
{
    [SerializeField] private TextMeshProUGUI textToPrint;

    public void ShowText(string _text)
    {
        textToPrint.text = _text;
    }
}
