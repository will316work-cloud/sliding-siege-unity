using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Fluent builder for <see cref="AnimationRequest"/>; obtained via
/// <see cref="AnimationCaller.Request"/>.
/// </summary>
public sealed class AnimationRequestBuilder
{
    #region Private Fields
    private readonly AnimationCaller _caller;

    private int    _stateHash;
    private int    _layerIndex;
    private float  _fadeTime;
    private float  _normalisedTime;
    private float  _speed;
    private string _speedParameterOverride;
    private Action _onPlay;
    private Action _onComplete;

    private bool _stateSet;
    private bool _timeSet;
    #endregion

    #region Constructor
    internal AnimationRequestBuilder(AnimationCaller caller)
    {
        _caller                 = caller;
        _layerIndex             = caller.DefaultLayerIndex;
        _fadeTime               = caller.DefaultFadeTime;
        _normalisedTime         = 0f;
        _speed                  = 1f;   // unspecified speed resets to 1 on dispatch
        _speedParameterOverride = null;
        _onPlay                 = null;
        _onComplete             = null;
    }
    #endregion

    #region State — string name
    /// <summary>Set the target state by its exact name in the Animator.</summary>
    public AnimationRequestBuilder WithState(string stateName)
    {
        _stateHash = Animator.StringToHash(stateName);
        _stateSet  = true;
        return this;
    }
    #endregion

    #region State — pre-hashed int
    /// <summary>Set the target state by a pre-computed hash.</summary>
    public AnimationRequestBuilder WithState(int stateHash)
    {
        _stateHash = stateHash;
        _stateSet  = true;
        return this;
    }
    #endregion

    #region Layer — index overloads
    /// <summary>Play on the given layer index.</summary>
    public AnimationRequestBuilder OnLayer(int layerIndex)
    {
        _layerIndex = layerIndex;
        return this;
    }
    #endregion

    #region Layer — name overloads
    /// <summary>Play on the layer with the given name (resolved via the Animator).</summary>
    public AnimationRequestBuilder OnLayer(string layerName)
    {
        _layerIndex = _caller.GetLayerIndex(layerName);
        return this;
    }
    #endregion

    #region Fade — seconds
    /// <summary>Crossfade over <paramref name="seconds"/>. Pass 0 for an instant transition.</summary>
    public AnimationRequestBuilder WithFade(float seconds)
    {
        _fadeTime = Mathf.Max(0f, seconds);
        return this;
    }
    #endregion

    #region Fade — instant shorthand
    /// <summary>Force an instant transition, overriding any default fade time.</summary>
    public AnimationRequestBuilder Instantly()
    {
        _fadeTime = 0f;
        return this;
    }
    #endregion

    #region Normalised time
    /// <summary>Start the destination state at a normalised time offset [0–1].</summary>
    public AnimationRequestBuilder AtTime(float normalisedTime)
    {
        _normalisedTime = Mathf.Clamp01(normalisedTime);
        _timeSet        = true;
        return this;
    }
    #endregion

    #region Speed
    /// <summary>
    /// Set the playback speed multiplier of the destination state. Any float:
    /// 1 = normal, 2 = double, 0.5 = half, negative plays BACKWARDS.
    /// Written to the speed parameter mapped to the target layer (see
    /// <see cref="AnimationCaller"/>'s layer speed parameters); the state's
    /// Speed Multiplier must be bound to that parameter in the Animator.
    /// When unspecified, the layer's speed parameter resets to 1 on dispatch.
    /// Negative speed with no explicit <see cref="AtTime"/> auto-starts at
    /// the end (normalised time 1) so the reverse playback is visible.
    /// </summary>
    public AnimationRequestBuilder WithSpeed(float speed)
    {
        _speed = speed;
        return this;
    }

    /// <summary>
    /// Write the speed to an explicit Animator float parameter instead of
    /// the one mapped to the target layer.
    /// </summary>
    public AnimationRequestBuilder WithSpeedParameter(string parameterName)
    {
        _speedParameterOverride = string.IsNullOrEmpty(parameterName) ? null : parameterName;
        return this;
    }
    #endregion

    #region Callback
    /// <summary>
    /// Register a one-shot <see cref="Action"/> invoked immediately after
    /// the transition is dispatched to the Animator.
    /// </summary>
    public AnimationRequestBuilder OnPlay(Action callback)
    {
        _onPlay += callback;
        return this;
    }

    /// <summary>UnityEvent-friendly overload — wraps a no-argument UnityAction.</summary>
    public AnimationRequestBuilder OnPlay(UnityEvent callback)
    {
        _onPlay += callback.Invoke;
        return this;
    }

    /// <summary>
    /// Register a one-shot <see cref="Action"/> invoked when the destination
    /// state FINISHES playing (normalised time reaches the end — or the
    /// start, for negative speeds — or the state is interrupted/replaced).
    /// </summary>
    public AnimationRequestBuilder OnComplete(Action callback)
    {
        _onComplete += callback;
        return this;
    }

    /// <summary>UnityEvent-friendly overload — wraps a no-argument UnityAction.</summary>
    public AnimationRequestBuilder OnComplete(UnityEvent callback)
    {
        _onComplete += callback.Invoke;
        return this;
    }
    #endregion

    #region Terminal — Play
    /// <summary>Build the request and dispatch it to the Animator immediately.</summary>
    public void Play()
    {
        AnimationRequest request = Build();
        _caller.Dispatch(request);
    }
    #endregion

    #region Terminal — Build
    /// <summary>
    /// Build an <see cref="AnimationRequest"/> without dispatching it.
    /// Useful for storing or deferring playback.
    /// </summary>
    public AnimationRequest Build()
    {
        if (!_stateSet)
            Debug.LogWarning("[AnimationCaller] AnimationRequestBuilder.Build() called without a state. " +
                             "Call WithState() before Build() or Play().", _caller);

        // Reverse playback from t=0 would clamp immediately; default to the end.
        float time = _normalisedTime;
        if (_speed < 0f && !_timeSet)
            time = 1f;

        return new AnimationRequest(
            _stateHash, _layerIndex,
            _fadeTime,  time,
            _speed, _speedParameterOverride,
            _onPlay, _onComplete);
    }
    #endregion
}
