using System;
using UnityEngine;

/// <summary>
/// Maps one Animator layer to the float parameter its states bind as their
/// Speed Multiplier. Author by layer index OR layer name (name is used when
/// index is negative).
/// </summary>
[Serializable]
public class LayerSpeedParameter
{
    [Tooltip("Layer index this mapping applies to. Use -1 to map by Layer Name instead.")]
    [SerializeField] private int _layerIndex = 0;

    [Tooltip("Layer name this mapping applies to (used when Layer Index is -1).")]
    [SerializeField] private string _layerName = "";

    [Tooltip("Animator float parameter that the layer's states bind as their Speed Multiplier.")]
    [SerializeField] private string _parameterName = "";

    private int _parameterHash;

    public int    LayerIndex    { get => _layerIndex; set => _layerIndex = value; }
    public string LayerName     { get => _layerName; set => _layerName = value; }
    public string ParameterName
    {
        get => _parameterName;
        set { _parameterName = value; BakeHash(); }
    }
    public int ParameterHash => _parameterHash;

    public LayerSpeedParameter() { }

    public LayerSpeedParameter(int layerIndex, string layerName, string parameterName)
    {
        _layerIndex = layerIndex;
        _layerName = layerName;
        _parameterName = parameterName;
        BakeHash();
    }

    public void BakeHash() =>
        _parameterHash = string.IsNullOrEmpty(_parameterName) ? 0 : Animator.StringToHash(_parameterName);
}
