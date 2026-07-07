using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fluent Animator dispatcher. Supports per-request playback speed
/// (including negative = backwards) via per-layer speed parameters: each
/// layer maps to a float parameter that its states bind as their Speed
/// Multiplier, so simultaneous animations on different layers can run at
/// different speeds. A request without an explicit speed resets its layer's
/// parameter to 1 on dispatch (other layers' parameters are untouched).
/// </summary>
[RequireComponent(typeof(Animator))]
public class AnimationCaller : MonoBehaviour
{
    #region Serialized Fields
    [Header("Presets")]
    [Tooltip("Named presets callable from the Inspector, UnityEvents, or code via PlayPreset().")]
    [SerializeField] private List<AnimationPreset> _presets = new();

    [Header("Defaults")]
    [Tooltip("Fallback layer index when no layer is specified on the builder.")]
    [SerializeField] private int _defaultLayerIndex = 0;

    [Tooltip("Fallback crossfade duration when no fade is specified on the builder.")]
    [Min(0f)]
    [SerializeField] private float _defaultFadeTime = 0f;

    [Header("Speed Parameters")]
    [Tooltip("Per-layer speed parameter mappings. Each layer's states bind the mapped " +
             "float parameter as their Speed Multiplier in the Animator window.")]
    [SerializeField] private List<LayerSpeedParameter> _layerSpeedParameters = new();

    [Tooltip("Fallback speed parameter for layers with no mapping. Empty = speed is skipped for those layers.")]
    [SerializeField] private string _defaultSpeedParameterName = "SpeedMultiplier";
    #endregion

    #region Private Fields
    private Animator _animator;
    private readonly Dictionary<string, int> _layerIndexByName = new();
    private int _defaultSpeedParameterHash;
    #endregion

    #region Properties
    /// <summary>The Animator driven by this caller.</summary>
    public Animator Animator => _animator;

    /// <summary>Read-only view of all Inspector-configured presets.</summary>
    public IReadOnlyList<AnimationPreset> Presets => _presets;

    /// <summary>Read-only view of the per-layer speed parameter mappings.</summary>
    public IReadOnlyList<LayerSpeedParameter> LayerSpeedParameters => _layerSpeedParameters;

    /// <summary>Fallback layer index when no layer is specified on the builder.</summary>
    public int DefaultLayerIndex
    {
        get => _defaultLayerIndex;
        set => _defaultLayerIndex = value;
    }

    /// <summary>Fallback crossfade duration when no fade is specified on the builder.</summary>
    public float DefaultFadeTime
    {
        get => _defaultFadeTime;
        set => _defaultFadeTime = Mathf.Max(0f, value);
    }

    /// <summary>Fallback speed parameter for layers with no mapping.</summary>
    public string DefaultSpeedParameterName
    {
        get => _defaultSpeedParameterName;
        set
        {
            _defaultSpeedParameterName = value;
            BakeDefaultSpeedHash();
        }
    }
    #endregion

    #region MonoBehaviour Callbacks
    private void Awake()
    {
        _animator = GetComponent<Animator>();
        BakeAllHashes();
        CacheLayerNames();
    }
    #endregion

    #region Public Methods

    // ── Builder entry-point ───────────────────────────────────────────────────

    /// <summary>
    /// Returns a fresh <see cref="AnimationRequestBuilder"/> pre-seeded with
    /// this caller's defaults. Chain fluent calls, then end with
    /// <c>.Play()</c> or <c>.Build()</c>.
    /// <example><code>
    /// caller.Request().WithState("Run").OnLayer("Body").WithSpeed(-1.5f).Play();
    /// </code></example>
    /// </summary>
    public AnimationRequestBuilder Request() => new AnimationRequestBuilder(this);

    // ── Preset API ────────────────────────────────────────────────────────────

    /// <summary>Play the preset whose Label matches <paramref name="label"/> (case-insensitive).</summary>
    public void PlayPreset(string label) => PlayPreset(label, null);

    /// <summary>
    /// Play a preset with an additional one-shot completion callback
    /// (invoked alongside the preset's own OnComplete UnityEvent).
    /// </summary>
    public void PlayPreset(string label, Action onComplete)
    {
        AnimationPreset preset = FindPreset(label);
        if (preset == null)
        {
            Debug.LogWarning($"[AnimationCaller] No preset with label '{label}'.", this);
            onComplete?.Invoke();
            return;
        }
        DispatchPreset(preset, onComplete);
    }

    /// <summary>Play a preset by its index in the Inspector list.</summary>
    public void PlayPresetByIndex(int index)
    {
        if (index < 0 || index >= _presets.Count)
        {
            Debug.LogWarning($"[AnimationCaller] Preset index {index} out of range.", this);
            return;
        }
        DispatchPreset(_presets[index], null);
    }

    // ── Direct dispatch ───────────────────────────────────────────────────────

    /// <summary>Dispatch a fully-constructed <see cref="AnimationRequest"/>.</summary>
    public void Dispatch(AnimationRequest request)
    {
        if (!enabled || request == null) return;

        ApplySpeed(request);

        if (request.FadeTime <= 0f)
            _animator.Play(request.StateHash, request.LayerIndex, request.NormalisedTime);
        else
            _animator.CrossFadeInFixedTime(
                request.StateHash, request.FadeTime,
                request.LayerIndex, 0f, request.NormalisedTime);

        request.OnPlay?.Invoke();

        if (request.OnComplete != null)
            StartCoroutine(WaitForStateComplete(request));
    }

    // ── Layer utility ─────────────────────────────────────────────────────────

    /// <summary>Returns the layer index for <paramref name="layerName"/>, or the default layer.</summary>
    public int GetLayerIndex(string layerName) => ResolveLayerIndex(layerName);

    // ── Speed parameter mapping API ───────────────────────────────────────────

    /// <summary>Add or replace the speed parameter mapping for a layer index.</summary>
    public void SetLayerSpeedParameter(int layerIndex, string parameterName)
    {
        var mapping = FindMappingByIndex(layerIndex);
        if (mapping != null) mapping.ParameterName = parameterName;
        else _layerSpeedParameters.Add(new LayerSpeedParameter(layerIndex, "", parameterName));
    }

    /// <summary>Add or replace the speed parameter mapping for a layer name.</summary>
    public void SetLayerSpeedParameter(string layerName, string parameterName)
    {
        SetLayerSpeedParameter(ResolveLayerIndex(layerName), parameterName);
    }

    #endregion

    #region Private Methods

    private void ApplySpeed(AnimationRequest request)
    {
        int hash;
        if (!string.IsNullOrEmpty(request.SpeedParameterOverride))
            hash = Animator.StringToHash(request.SpeedParameterOverride);
        else
            hash = ResolveSpeedParameterHash(request.LayerIndex);

        if (hash == 0) return; // no mapping and no default: speed unsupported here
        _animator.SetFloat(hash, request.Speed);
    }

    private int ResolveSpeedParameterHash(int layerIndex)
    {
        var mapping = FindMappingByIndex(layerIndex);
        if (mapping != null && mapping.ParameterHash != 0) return mapping.ParameterHash;
        return _defaultSpeedParameterHash;
    }

    private LayerSpeedParameter FindMappingByIndex(int layerIndex)
    {
        foreach (var m in _layerSpeedParameters)
        {
            int mappedIndex = m.LayerIndex >= 0 ? m.LayerIndex : ResolveLayerIndexQuiet(m.LayerName);
            if (mappedIndex == layerIndex) return m;
        }
        return null;
    }

    private void DispatchPreset(AnimationPreset preset, Action onComplete = null)
    {
        int layer = preset.LayerIndex < 0 ? _defaultLayerIndex : preset.LayerIndex;

        var builder = Request()
            .WithState(preset.StateHash)
            .OnLayer(layer)
            .WithFade(preset.FadeTime)
            .WithSpeed(preset.Speed)
            .WithSpeedParameter(preset.SpeedParameterOverride)
            .OnPlay(preset.OnPlay)
            .OnComplete(preset.OnComplete);
        if (onComplete != null) builder.OnComplete(onComplete);

        // Preset time of 0 with negative speed defers to the builder's
        // auto-start-at-end; any other value is explicit.
        if (!(preset.Speed < 0f && Mathf.Approximately(preset.NormalisedTime, 0f)))
            builder.AtTime(preset.NormalisedTime);

        builder.Play();
    }

    /// <summary>
    /// Waits until the dispatched state has finished playing on its layer:
    /// its normalised time passes the end (or the start, for negative
    /// speeds), or the Animator has moved on to a different state
    /// (interruption also counts as completion so callbacks always fire).
    /// </summary>
    private IEnumerator WaitForStateComplete(AnimationRequest request)
    {
        // Let the Play/CrossFade register with the Animator first.
        yield return null;

        bool reverse = request.Speed < 0f;
        while (enabled && _animator != null && _animator.isActiveAndEnabled)
        {
            bool inTransition = _animator.IsInTransition(request.LayerIndex);
            var info = inTransition
                ? _animator.GetNextAnimatorStateInfo(request.LayerIndex)
                : _animator.GetCurrentAnimatorStateInfo(request.LayerIndex);

            bool isOurState = info.shortNameHash == request.StateHash ||
                              info.fullPathHash == request.StateHash;

            if (!isOurState)
            {
                // Interrupted or replaced — treat as complete.
                break;
            }

            if (!inTransition)
            {
                if (!reverse && info.normalizedTime >= 1f) break;
                if (reverse && info.normalizedTime <= 0f) break;
            }

            yield return null;
        }

        request.OnComplete?.Invoke();
    }

    private void BakeAllHashes()
    {
        foreach (var preset in _presets)
            preset.BakeHash();
        foreach (var mapping in _layerSpeedParameters)
            mapping.BakeHash();
        BakeDefaultSpeedHash();
    }

    private void BakeDefaultSpeedHash() =>
        _defaultSpeedParameterHash = string.IsNullOrEmpty(_defaultSpeedParameterName)
            ? 0 : Animator.StringToHash(_defaultSpeedParameterName);

    private void CacheLayerNames()
    {
        _layerIndexByName.Clear();
        for (int i = 0; i < _animator.layerCount; i++)
            _layerIndexByName[_animator.GetLayerName(i)] = i;
    }

    private int ResolveLayerIndex(string layerName)
    {
        if (_layerIndexByName.TryGetValue(layerName, out int index)) return index;
        Debug.LogWarning($"[AnimationCaller] Layer '{layerName}' not found. Using default ({_defaultLayerIndex}).", this);
        return _defaultLayerIndex;
    }

    private int ResolveLayerIndexQuiet(string layerName)
    {
        if (!string.IsNullOrEmpty(layerName) && _layerIndexByName.TryGetValue(layerName, out int index)) return index;
        return -1;
    }

    private AnimationPreset FindPreset(string label)
    {
        foreach (var preset in _presets)
            if (string.Equals(preset.Label, label, StringComparison.OrdinalIgnoreCase))
                return preset;
        return null;
    }

    #endregion
}
