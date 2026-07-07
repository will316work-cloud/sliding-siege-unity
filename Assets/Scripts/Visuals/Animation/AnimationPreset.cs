using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Inspector-defined animation preset; builds a request on demand via
/// <see cref="AnimationCaller.PlayPreset(string)"/>.
/// </summary>
[Serializable]
public class AnimationPreset
{
    [Tooltip("Friendly name used to look up this preset via PlayPreset().")]
    [SerializeField] private string _label = "NewPreset";

    [Tooltip("Exact state name as it appears in the Animator (hashed on Awake).")]
    [SerializeField] private string _stateName = "";

    [Tooltip("Layer index. Use -1 to inherit the AnimationCaller default.")]
    [SerializeField] private int _layerIndex = 0;

    [Tooltip("Crossfade duration in seconds. Use 0 for an instant transition.")]
    [Min(0f)]
    [SerializeField] private float _fadeTime = 0f;

    [Tooltip("Normalised time offset within the destination state (0 = start). " +
             "Ignored (auto = 1) when Speed is negative and this is left at 0.")]
    [Range(0f, 1f)]
    [SerializeField] private float _normalisedTime = 0f;

    [Tooltip("Playback speed multiplier: 1 = normal, negative = backwards. " +
             "Written to the layer's mapped speed parameter (or the override below).")]
    [SerializeField] private float _speed = 1f;

    [Tooltip("Optional explicit speed parameter name; empty = use the layer mapping.")]
    [SerializeField] private string _speedParameterOverride = "";

    [Header("Events")]
    [SerializeField] private UnityEvent _onPlay = new();
    [SerializeField] private UnityEvent _onComplete = new();

    private int _stateHash;

    public string Label                  => _label;
    public string StateName              => _stateName;
    public int    LayerIndex             => _layerIndex;
    public float  FadeTime               => _fadeTime;
    public float  NormalisedTime         => _normalisedTime;
    public float  Speed                  => _speed;
    public string SpeedParameterOverride => _speedParameterOverride;
    public UnityEvent OnPlay             => _onPlay;
    public UnityEvent OnComplete         => _onComplete;
    public int    StateHash              => _stateHash;

    public void BakeHash() => _stateHash = Animator.StringToHash(_stateName);
}
