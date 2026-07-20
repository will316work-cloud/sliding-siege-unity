using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Events;

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
    [SerializeField] private string _defaultSpeedParameterName = "Speed Multiplier";

    [Header("Animation Event Parameters")]
    [SerializeField] private AnimationEventParameters[] animationEvents;


    #endregion

    #region Private Fields
    private Animator _animator;
    private readonly Dictionary<string, int> _layerIndexByName = new();
    private readonly HashSet<int> _existingParameterHashes = new();
    private readonly HashSet<int> _warnedMissingParameters = new();
    private int _defaultSpeedParameterHash;
    /// <summary>The most recently dispatched request — used by WaitForStateComplete
    /// to tell "a newer Dispatch() truly superseded us" apart from "the Animator
    /// just hasn't caught up to our own Play() yet" (see its remarks).</summary>
    private AnimationRequest _activeRequest;
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
        CacheParameterHashes();
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
    public void PlayPreset(string label, Action onComplete) => PlayPreset(label, 1f, onComplete);

    /// <summary>
    /// Play a preset with its speed multiplied by <paramref name="speedScale"/>
    /// (e.g. to fit the clip into a specific real-time duration) and an
    /// optional completion callback.
    /// </summary>
    public void PlayPreset(string label, float speedScale, Action onComplete = null)
    {
        AnimationPreset preset = FindPreset(label);
        if (preset == null)
        {
            Debug.LogWarning($"[AnimationCaller] No preset with label '{label}'.", this);
            onComplete?.Invoke();
            return;
        }
        DispatchPreset(preset, onComplete, speedScale);
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

        _activeRequest = request;

        int speedHash = ApplySpeed(request);

        if (request.FadeTime <= 0f)
            _animator.Play(request.StateHash, request.LayerIndex, request.NormalisedTime);
        else
            _animator.CrossFadeInFixedTime(
                request.StateHash, request.FadeTime,
                request.LayerIndex, 0f, request.NormalisedTime);

        // Force the transition to actually apply THIS frame. Helps most
        // cases, but the very first Animator ever bound to a given
        // AnimatorController in the scene (e.g. the pool's first-ever
        // piece) can still take an extra frame or two for its playable
        // graph to catch up even after this call — see the interruption
        // check in WaitForStateComplete for how that's handled.
        _animator.Update(0f);

        request.OnPlay?.Invoke();

        // Watch for completion whenever a caller wants the callback, OR
        // whenever this request drove the speed parameter away from its
        // neutral default (1) — the reset below needs the wait either way,
        // even for fire-and-forget presets with no OnComplete.
        bool needsSpeedReset = speedHash != 0 && !Mathf.Approximately(request.Speed, 1f);
        if (request.OnComplete != null || needsSpeedReset)
            StartCoroutine(WaitForStateComplete(request, speedHash));
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

    /// <summary>
    /// Re-reads the Animator's current layer names and float parameters.
    /// Call this whenever something swaps <c>Animator.runtimeAnimatorController</c>
    /// at runtime (e.g. per-enemy-definition controller overrides) — the
    /// caches built at Awake() describe whichever controller was bound at
    /// that moment and go stale the instant a different one is assigned.
    /// </summary>
    public void RefreshAnimatorBindings()
    {
        CacheLayerNames();
        CacheParameterHashes();
    }

    /// <summary>
    /// Rebinds the Animator and drops the state machine back to the
    /// controller's default state. For pooled objects on reuse: states with
    /// Write Defaults off and no exit transition (e.g. Enemy Death) keep
    /// re-sampling their final frame forever, and the pool's disable/enable
    /// cycle is not guaranteed to rebind when an object is released and
    /// re-acquired in quick succession.
    /// </summary>
    public void ResetToDefaultState()
    {
        if (_animator == null) _animator = GetComponent<Animator>();
        if (_animator == null || !_animator.isActiveAndEnabled) return;
        _activeRequest = null; // stale request from a previous life
        _animator.Rebind();
        _animator.Update(0f);
    }

    public void CallAnimationEvent(string label)
    {
        foreach (AnimationEventParameters parameters in animationEvents)
        {
            if (parameters.MatchingLabel(label))
            {
                parameters.CallEvent();
                break;
            }
        }
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Writes the request's speed to its resolved Animator float parameter
    /// and returns the parameter's hash (0 if speed isn't supported for this
    /// request — no mapping and no default). The existence cache is used
    /// only to decide whether to log a one-time diagnostic warning; it must
    /// NOT gate the SetFloat call itself; a stale/empty cache (e.g. read
    /// before the Animator's parameters were available) would otherwise
    /// silently skip applying a perfectly valid speed.
    /// </summary>
    private int ApplySpeed(AnimationRequest request)
    {
        int hash;
        if (!string.IsNullOrEmpty(request.SpeedParameterOverride))
            hash = Animator.StringToHash(request.SpeedParameterOverride);
        else
            hash = ResolveSpeedParameterHash(request.LayerIndex);

        if (hash == 0) return 0; // no mapping and no default: speed unsupported here

        if (!_existingParameterHashes.Contains(hash) && _warnedMissingParameters.Add(hash))
            Debug.LogWarning("[AnimationCaller] Speed parameter not found on this Animator — " +
                             "add the float parameter and bind it as the state's Speed Multiplier, " +
                             "or speed requests will have no effect.", this);

        _animator.SetFloat(hash, request.Speed); // safe no-op if the parameter truly doesn't exist
        return hash;
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

    private void DispatchPreset(AnimationPreset preset, Action onComplete = null, float speedScale = 1f)
    {
        int layer = preset.LayerIndex < 0 ? _defaultLayerIndex : preset.LayerIndex;

        var builder = Request()
            .WithState(preset.StateHash)
            .OnLayer(layer)
            .WithFade(preset.FadeTime)
            .WithSpeed(preset.Speed * speedScale)
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
    /// Afterwards, resets the driven speed parameter back to its neutral
    /// default (1) — unless a newer request already re-drove the same
    /// parameter to a different value in the meantime, in which case
    /// resetting here would incorrectly clobber it.
    /// </summary>
    private IEnumerator WaitForStateComplete(AnimationRequest request, int speedHash)
    {
        // Let the Play/CrossFade register with the Animator first.
        yield return null;

        bool reverse = request.Speed < 0f;
        // The very first Animator ever bound to a given AnimatorController
        // in the scene (e.g. the object pool's first-ever piece) can take
        // several extra frames for its playable graph to catch up to a
        // Play() call, even after Dispatch()'s forced Update(0f) — cap how
        // long we tolerate "not there yet" before giving up so a genuinely
        // broken Animator can't hang this coroutine forever.
        int staleFramesLeft = 120;
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
                // A newer Dispatch() truly superseded us -> done. Otherwise
                // this is still our own request — the Animator just hasn't
                // caught up to it yet — so keep waiting instead of bailing
                // and firing OnComplete (and any Idle resume) prematurely.
                if (_activeRequest != request || --staleFramesLeft <= 0) break;
                yield return null;
                continue;
            }

            if (!inTransition)
            {
                if (!reverse && info.normalizedTime >= 1f) break;
                if (reverse && info.normalizedTime <= 0f) break;
            }

            yield return null;
        }

        if (speedHash != 0 && _animator != null && !Mathf.Approximately(request.Speed, 1f)
            && Mathf.Approximately(_animator.GetFloat(speedHash), request.Speed))
        {
            _animator.SetFloat(speedHash, 1f);
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

    private void CacheParameterHashes()
    {
        if (_animator == null || _animator.runtimeAnimatorController == null) return;
        _existingParameterHashes.Clear();
        foreach (var p in _animator.parameters)
            if (p.type == AnimatorControllerParameterType.Float)
                _existingParameterHashes.Add(p.nameHash);
    }

    private void CacheLayerNames()
    {
        if (_animator == null || _animator.runtimeAnimatorController == null) return;
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
