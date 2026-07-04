// ============================================================
// DEV HELPERS  (NEW in Bunch 2 — temporary tooling)
//
// DevTestSpawner: there is no spawn pool (Bunch 9) or debug panel
// (Bunch 12) yet, so this presses enemies onto the board for
// testing shifts, linked shifts, wrap, revert and click-cycling.
// Press T in Play Mode (or use the component context menu).
// Remove/disable once Bunch 12's debug spawn ships.
//
// ConsoleFallbackLogger: toast()/log() views arrive in Bunch 10;
// until then this mirrors GameEvents.Toast/Log to the Console so
// every guard message ("No reverts left this turn!" etc.) is
// visible. Disable it in Bunch 10.
// ============================================================

using UnityEngine;
using UnityEngine.InputSystem;

public class DevTestSpawner : MonoBehaviour
{
    private void Update()
    {
        // New Input System (Active Input Handling = Input System Package).
        if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
            SpawnRandom();
    }

    [ContextMenu("Spawn Random Enemy")]
    public void SpawnRandom()
    {
        var s = GameManager.S;
        if (s == null || s.EnemyPhaseActive) return;
        if (Registry.Enemies.Count == 0)
        {
            Debug.LogWarning("DevTestSpawner: no EnemyDefinition assets in the DefinitionDatabase yet.");
            return;
        }

        string key = Registry.RandomKey(Registry.Enemies);
        var def = Registry.Enemies[key];

        // Find a legal anchor: every footprint cell (with wrap) must be free
        // of non-transparent occupants unless this type is itself transparent.
        for (int attempt = 0; attempt < 60; attempt++)
        {
            int r = Random.Range(0, s.Rows);
            int c = Random.Range(0, s.Cols);
            bool blocked = false;
            if (!def.IsTransparent)
            {
                for (int dr = 0; dr < def.BaseSize.x && !blocked; dr++)
                    for (int dc = 0; dc < def.BaseSize.y && !blocked; dc++)
                        if (GridLogic.CellHasNonTransparentOccupant((r + dr) % s.Rows, (c + dc) % s.Cols))
                            blocked = true;
            }
            if (blocked) continue;

            var enemy = def.CreateInstance();
            enemy.Id = s.NextId++;
            GridLogic.PlaceEnemyAt(r, c, enemy);
            GameEvents.Log($"[dev] Spawned {def.Label} #{enemy.Id} at ({r},{c})");
            GameEvents.RaiseStateChanged();
            return;
        }
        GameEvents.Toast("No room to spawn a " + def.Label + "!");
    }
}

public class ConsoleFallbackLogger : MonoBehaviour
{
    private void OnEnable()
    {
        GameEvents.ToastRequested += OnToast;
        GameEvents.LogRequested += OnLog;
    }
    private void OnDisable()
    {
        GameEvents.ToastRequested -= OnToast;
        GameEvents.LogRequested -= OnLog;
    }
    private void OnToast(string msg) => Debug.Log("[TOAST] " + msg);
    private void OnLog(string msg) => Debug.Log("[LOG] " + msg);
}
