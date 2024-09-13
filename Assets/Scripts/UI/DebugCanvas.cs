using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class DebugCanvas : Singleton<DebugCanvas>
{
    [SerializeField] private TextMeshProUGUI textToPrint;

    public void ShowText(string _text)
    {
        textToPrint.text = _text;
    }

    public void ResetScene()
    {
        SceneManager.LoadScene(0);
    }
}
