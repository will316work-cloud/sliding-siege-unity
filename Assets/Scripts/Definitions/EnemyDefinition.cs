// ============================================================
// ENEMY DEFINITION (ScriptableObject)
// (*** PATCHED in Bunch 2 — replaces the Bunch 1 file ***)
// CHANGE LOG vs Bunch 1: CreateInstance() seeds the runtime
// Enemy.Size from BaseSize (Enemy no longer carries BaseSize).
// See Bunch 1 header for full translation notes.
// ============================================================

using UnityEngine;

[CreateAssetMenu(menuName = "GridTactics/Enemy Definition", fileName = "EnemyDef_")]
public class EnemyDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Registry key, e.g. \"phantom\". Must match the JS type string exactly.")]
    public string Key;
    public string Label;

    [TextArea(3, 8)]
    public string Description;

    [Header("Stats")]
    public int BaseHealth;

    [Tooltip("Footprint in cells: x = rows, y = cols. JS baseSize [2,2] → (2,2).")]
    public Vector2Int BaseSize = new Vector2Int(1, 1);

    [Header("Flags (JS constructor statics)")]
    public bool IsTransparent;
    public bool CountsTowardGridFull = true;

    [Header("Visuals (art comes later — placeholders OK)")]
    public Sprite Sprite;
    public string IconText;
    public Color Tint = Color.white;

    /// <summary>JS: ENEMY_CONSTRUCTORS[type](hp). Id/Anchor assigned by the
    /// spawner via GridLogic.PlaceEnemyAt. Bunches 3–5 layer per-type init
    /// on top via EnemyBehaviour.InitInstance.</summary>
    public Enemy CreateInstance(int? hp = null)
    {
        return new Enemy
        {
            Type = Key,
            Label = Label,
            Hp = hp ?? BaseHealth,
            MaxHp = hp ?? BaseHealth,
            Size = BaseSize
        };
    }
}
