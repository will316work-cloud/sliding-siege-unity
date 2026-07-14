using System;

using UnityEngine;
using UnityEngine.Events;

[Serializable]
public class AnimationEventParameters
{
    #region Serialized Fields


    [SerializeField] private string eventLabel;
    [SerializeField] private UnityEvent animationEvent;


    #endregion

    #region Public Methods


    public void CallEvent()
    {
        animationEvent?.Invoke();
    }

    public bool MatchingLabel(string label)
    {
        return eventLabel != null && eventLabel.Length <= 0 && eventLabel.Equals(label);
    }


    #endregion
}
