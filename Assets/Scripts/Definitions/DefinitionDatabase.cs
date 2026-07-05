// ============================================================
// DEFINITION DATABASE + REGISTRY
// (*** PATCHED in Bunch 3 — replaces the Bunch 1 file ***)
// CHANGE LOG vs Bunch 1: Registry gains the Behaviours dictionary
// (the code half of ENEMY_CONSTRUCTORS), RegisterBehaviour(), and
// GetBehaviour(). See Bunch 1 header for the original notes.
// ============================================================

using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "GridTactics/Definition Database", fileName = "DefinitionDatabase")]
public class DefinitionDatabase : ScriptableObject
{
    public List<AttackDefinition> Attacks = new List<AttackDefinition>();
    public List<ItemDefinition> Items = new List<ItemDefinition>();
    public List<EnemyDefinition> Enemies = new List<EnemyDefinition>();
}

public static class Registry
{
    public static readonly Dictionary<string, AttackDefinition> Attacks = new Dictionary<string, AttackDefinition>();
    public static readonly Dictionary<string, ItemDefinition> Items = new Dictionary<string, ItemDefinition>();
    public static readonly Dictionary<string, EnemyDefinition> Enemies = new Dictionary<string, EnemyDefinition>();

    /// <summary>The behavioral half of ENEMY_CONSTRUCTORS. Populated by
    /// EnemyBehaviours.RegisterAll() from GameManager.Awake().</summary>
    public static readonly Dictionary<string, EnemyBehaviour> Behaviours = new Dictionary<string, EnemyBehaviour>();

    public static void Build(DefinitionDatabase db)
    {
        Attacks.Clear();
        Items.Clear();
        Enemies.Clear();
        Behaviours.Clear();

        foreach (var a in db.Attacks)
        {
            if (a == null || string.IsNullOrEmpty(a.Key)) { Debug.LogError("Attack asset missing or has empty Key"); continue; }
            if (!Attacks.TryAdd(a.Key, a)) Debug.LogError($"Duplicate attack key: {a.Key}");
        }
        foreach (var i in db.Items)
        {
            if (i == null || string.IsNullOrEmpty(i.Key)) { Debug.LogError("Item asset missing or has empty Key"); continue; }
            if (!Items.TryAdd(i.Key, i)) Debug.LogError($"Duplicate item key: {i.Key}");
        }
        foreach (var e in db.Enemies)
        {
            if (e == null || string.IsNullOrEmpty(e.Key)) { Debug.LogError("Enemy asset missing or has empty Key"); continue; }
            if (!Enemies.TryAdd(e.Key, e)) Debug.LogError($"Duplicate enemy key: {e.Key}");
        }
    }

    public static void RegisterBehaviour(EnemyBehaviour behaviour)
    {
        if (behaviour == null || string.IsNullOrEmpty(behaviour.Key))
        { Debug.LogError("EnemyBehaviour with missing key"); return; }
        if (!Behaviours.TryAdd(behaviour.Key, behaviour))
            Debug.LogError($"Duplicate behaviour key: {behaviour.Key}");
    }

    /// <summary>Null = no registered behaviour (pure-generic enemy).</summary>
    public static EnemyBehaviour GetBehaviour(string key)
        => Behaviours.TryGetValue(key, out var b) ? b : null;

    public static string RandomKey<T>(Dictionary<string, T> dict)
    {
        var keys = new List<string>(dict.Keys);
        return keys.Count == 0 ? null : keys[Random.Range(0, keys.Count)];
    }
}
