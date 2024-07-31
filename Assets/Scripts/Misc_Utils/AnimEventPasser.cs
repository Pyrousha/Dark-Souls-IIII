using UnityEngine;
using UnityEngine.Events;

public class AnimEventPasser : MonoBehaviour
{
    [SerializeField] private UnityEvent m_event;

    public void DoEvent()
    {
        m_event?.Invoke();
    }
}
