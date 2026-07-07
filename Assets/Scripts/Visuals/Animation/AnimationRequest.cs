using System;

/// <summary>
/// Immutable animation request value, built by <see cref="AnimationRequestBuilder"/>
/// and dispatched by <see cref="AnimationCaller"/>.
/// </summary>
public sealed class AnimationRequest
{
    private readonly int    _stateHash;
    private readonly int    _layerIndex;
    private readonly float  _fadeTime;
    private readonly float  _normalisedTime;
    private readonly float  _speed;
    private readonly string _speedParameterOverride;
    private readonly Action _onPlay;
    private readonly Action _onComplete;

    internal int    StateHash              => _stateHash;
    internal int    LayerIndex             => _layerIndex;
    internal float  FadeTime               => _fadeTime;
    internal float  NormalisedTime         => _normalisedTime;
    /// <summary>Playback speed multiplier (1 = normal, negative = backwards).</summary>
    internal float  Speed                  => _speed;
    /// <summary>Explicit speed parameter name; null = resolve from the layer mapping.</summary>
    internal string SpeedParameterOverride => _speedParameterOverride;
    /// <summary>Invoked immediately after the transition is dispatched.</summary>
    internal Action OnPlay                 => _onPlay;
    /// <summary>Invoked once the destination state finishes playing.</summary>
    internal Action OnComplete             => _onComplete;

    internal AnimationRequest(
        int stateHash, int layerIndex,
        float fadeTime, float normalisedTime,
        float speed, string speedParameterOverride,
        Action onPlay, Action onComplete)
    {
        _stateHash              = stateHash;
        _layerIndex             = layerIndex;
        _fadeTime               = fadeTime;
        _normalisedTime         = normalisedTime;
        _speed                  = speed;
        _speedParameterOverride = speedParameterOverride;
        _onPlay                 = onPlay;
        _onComplete             = onComplete;
    }
}
