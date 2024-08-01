using UnityEngine;
using UnityEngine.Events;

public class AnimEventPasser : MonoBehaviour
{
    [SerializeField] private UnityEvent[] m_events;

    public void DoEvent(int index)
    {
        m_events[index]?.Invoke();
    }
}
