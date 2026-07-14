using MilkShake;
using System.Collections.Generic;

using UnityEngine;

public class ShakerCaller : MonoBehaviour
{
    #region Serialized Fields


    [SerializeField] private Shaker _shaker;


    #endregion

    #region


    private Dictionary<ShakePreset, ShakeInstance> _shakeInstances;


    #endregion

    #region MonoBehavior Callbacks


    private void Awake()
    {
        if (_shaker == null) 
            _shaker = GetComponent<Shaker>();

        _shakeInstances = new Dictionary<ShakePreset, ShakeInstance>();
    }


    #endregion

    #region Public Methods


    public void PlayMilkShake(ShakePreset preset)
    {
        if (_shaker != null && preset != null)
        {
            ShakeInstance instance = _shaker.Shake(preset);

            if (_shakeInstances.ContainsKey(preset))
                _shakeInstances[preset] = instance;
            else
                _shakeInstances.Add(preset, instance);
        }
        else
            Debug.LogWarning($"{name} can't shake due to not having a shaker or shake preset.");
    }

    public void StopMilkShake(ShakePreset shakeOrigin)
    {
        if (_shakeInstances.ContainsKey(shakeOrigin) && _shakeInstances[shakeOrigin] != null)
            _shakeInstances[shakeOrigin].Stop(shakeOrigin.FadeOut, true);
        else
            Debug.LogWarning($"{name} does not have an active shake from this preset.");
    }


    #endregion
}
