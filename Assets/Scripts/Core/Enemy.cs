// ============================================================
// ENEMY (runtime instance)
// (*** PATCHED in Bunch 4 — replaces the Bunch 3 file ***)
// CHANGE LOG vs Bunch 3:
//   + ClusterId    (JS: en.clusterId — slime cluster key)
//   + MustOneShot  (JS: en.mustOneShot — slime cluster mechanic flag)
//   + StretchAnim  (JS: en._stretchAnim — 'rolly-stretch-grow' |
//                   'rolly-stretch-shrink' | null; rendering reads it)
// All other fields unchanged — see Bunch 3 header.
// ============================================================

using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Enemy
{
    public int Id;
    public string Type;
    public string Label;

    public int Hp;
    public int MaxHp;

    public Vector2Int Anchor;
    public Vector2Int Size = new Vector2Int(1, 1);

    public string Variant;
    public List<int> LinkedIds = new List<int>();

    // ---- Rolly stretch state ----
    public string StretchAxis = null;
    public int StretchBefore = 0;
    public int StretchAfter = 0;
    /// <summary>JS: en._stretchAnim — transient animation hint read by
    /// EnemyLayerView/RollyBlockRenderer to play grow/shrink scale.</summary>
    public string StretchAnim = null;

    // ---- Flags read by rendering ----
    public bool PendingSpawn = false;
    public bool PendingDetonation = false;
    public bool PendingHitThisCycle = false;

    // ---- Per-type fields ----
    public int SongCounter = 0;
    public string QueuedShape = null;
    public string DisabledAttackKey = null;

    // ---- Slime ----
    public int ClusterId = 0;
    public bool MustOneShot = false;
}
