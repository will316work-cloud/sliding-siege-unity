// ============================================================
// GAME MANAGER (MonoBehaviour singleton)
// Translated from: main.js — the entry point. Owns the one
// GameState, seeds it from the definition registry (replacing
// newRunState()'s hardcoded charge/item tables), and runs the
// init() sequence as a coroutine.
//
// SCENE SETUP (see the setup instructions for this bunch):
//   Put this on an empty root GameObject named "GameManager" in
//   the Game scene and assign the DefinitionDatabase asset.
// ============================================================

using System.Collections;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    /// <summary>Global state shortcut. C# equivalent of the JS global `state`.
    /// Usage everywhere: GameManager.S.Charges["axe"] etc.</summary>
    public static GameState S => Instance != null ? Instance.State : null;

    [SerializeField] private DefinitionDatabase database;

    public GameState State { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (database == null)
        {
            Debug.LogError("GameManager: DefinitionDatabase asset not assigned.");
            return;
        }
        Registry.Build(database);
    }

    private void Start()
    {
        // JS: init(); — called at the bottom of main.js after all scripts loaded.
        StartCoroutine(InitRun());
    }

    /// <summary>JS: newRunState(). Hardcoded tables become registry-driven seeding.</summary>
    public GameState NewRunState()
    {
        var s = new GameState();

        foreach (var kv in Registry.Attacks)
        {
            s.Charges[kv.Key] = kv.Value.StartingCharges;      // JS: charges: { axe: 2, ... }
            s.AttackBaseDmg[kv.Key] = kv.Value.BaseDmg;        // pristine copy; shop upgrades mutate this
        }
        foreach (var kv in Registry.Items)
            s.Items[kv.Key] = kv.Value.StartingUses;           // JS: items: { extraSwing: 2, ... }

        return s;
    }

    /// <summary>JS: async function init(). The floor-setup animation arrives in
    /// Bunch 9 (FloorController.AnimateFloorSetup); until then this boots an
    /// empty board so every earlier bunch is testable in Play Mode.</summary>
    public IEnumerator InitRun()
    {
        Session.Clear();
        State = NewRunState();
        State.EnemyPhaseActive = true;

        // Bunch 9 replaces this line with:
        //   yield return FloorController.Instance.AnimateFloorSetup(1);
        yield return null;

        State.EnemyPhaseActive = false;
        GameEvents.RaiseStateChanged();   // JS: render();
    }

    /// <summary>JS: the restart button's confirmed path — wipe and re-init.</summary>
    public void RestartRun()
    {
        StopAllCoroutines();
        StartCoroutine(InitRun());
    }
}
