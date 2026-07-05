// ============================================================
// STATUS HOOKS  (NEW in Bunch 3)
// The generic enemy code calls status checks owned by later
// bunches: isStunned() (stun-logic.js, Bunch 8), isVulnerable()
// (vulnerability-logic.js, Bunch 8), hasDamageReduction()
// (damage-reduction-logic.js, Bunch 8), and the ghost/phantom
// teleport pass (Bunch 5). Defaults = "system inert", identical
// to a fresh JS run before those effects exist.
// ============================================================

using System;
using System.Collections;

public static class StatusHooks
{
    public static Func<Enemy, bool> IsStunned = en => false;             // Bunch 8
    public static Func<Enemy, bool> IsVulnerable = en => false;          // Bunch 8
    public static Func<Enemy, bool> HasDamageReduction = en => false;    // Bunch 8

    /// <summary>JS: resolveGhostPhantomTeleportsSequenced() — runs at the end
    /// of the generic movement pass. Bunch 5 assigns. Must be a coroutine.</summary>
    public static Func<IEnumerator> GhostPhantomTeleportResolver = null;
}
