// ============================================================
// GAME MANAGER  (*** PATCHED in Bunch 3 — replaces the Bunch 1 file ***)
// CHANGE LOG vs Bunch 1:
//   + Awake() calls EnemyBehaviours.RegisterAll() after Registry.Build
//     (the C# stand-in for index.html's script load order).
//   + static Run(IEnumerator) so static logic classes (attack
//     resolution, turn cycle) can start coroutines — the C# home
//     for every JS `await`.
// See Bunch 1 header for the original translation notes.
// ============================================================

using System.Collections;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
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
        EnemyBehaviours.RegisterAll();   // NEW — behavioral registry
    }

    private void Start()
    {
        StartCoroutine(InitRun());
    }

    /// <summary>Coroutine host for static logic classes. JS: `await fn()`
    /// anywhere → `yield return ...` inside a routine started here.</summary>
    public static Coroutine Run(IEnumerator routine) => Instance.StartCoroutine(routine);

    public GameState NewRunState()
    {
        var s = new GameState();
        foreach (var kv in Registry.Attacks)
        {
            s.Charges[kv.Key] = kv.Value.StartingCharges;
            s.AttackBaseDmg[kv.Key] = kv.Value.BaseDmg;
        }
        foreach (var kv in Registry.Items)
            s.Items[kv.Key] = kv.Value.StartingUses;
        return s;
    }

    public IEnumerator InitRun()
    {
        Session.Clear();
        State = NewRunState();
        State.EnemyPhaseActive = true;

        // Bunch 9 replaces with: yield return FloorController.AnimateFloorSetup(1);
        yield return null;

        State.EnemyPhaseActive = false;
        GameEvents.RaiseStateChanged();
    }

    public void RestartRun()
    {
        StopAllCoroutines();
        StartCoroutine(InitRun());
    }
}
