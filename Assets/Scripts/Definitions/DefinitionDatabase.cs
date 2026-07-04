// ============================================================
// DEFINITION DATABASE + REGISTRY
// The C# replacement for the JS self-registration pattern
// (every module file doing ENEMY_CONSTRUCTORS.x = ... /
// ATTACK_DEFS.x = ... at script-load time, ordered by index.html).
//
// In Unity, "load order via script tags" becomes: drag every
// definition asset into ONE DefinitionDatabase asset, assign that
// to GameManager, and BuildRegistries() runs once in Awake().
// Deterministic, inspectable, and adding content = adding an
// asset to a list.
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

/// <summary>Static lookup tables — the direct equivalents of ATTACK_DEFS,
/// ITEM_DEFS and ENEMY_CONSTRUCTORS. Populated once by GameManager.</summary>
public static class Registry
{
    public static readonly Dictionary<string, AttackDefinition> Attacks = new Dictionary<string, AttackDefinition>();
    public static readonly Dictionary<string, ItemDefinition> Items = new Dictionary<string, ItemDefinition>();
    public static readonly Dictionary<string, EnemyDefinition> Enemies = new Dictionary<string, EnemyDefinition>();

    // Bunch 3 adds: Dictionary<string, EnemyBehaviour> Behaviours —
    // the constructor-function / phase-logic half of ENEMY_CONSTRUCTORS.

    public static void Build(DefinitionDatabase db)
    {
        Attacks.Clear();
        Items.Clear();
        Enemies.Clear();

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

    /// <summary>JS: randomKey(obj) in shop-logic.js — random key from a registry.</summary>
    public static string RandomKey<T>(Dictionary<string, T> dict)
    {
        var keys = new List<string>(dict.Keys);
        return keys.Count == 0 ? null : keys[Random.Range(0, keys.Count)];
    }
}
