using UnityEngine;

namespace SlidingSiege
{
    /// Strategy asset holding an enemy type's combat rules — the behaviors
    /// that used to be loose bools on EnemyDefinition plus the routing/
    /// zero-HP logic that used to live inside CombatSystem. Definitions
    /// reference one shared asset (Bomb, Golem, Slime rules); definitions
    /// without one use Default. Subclass and override the virtual methods
    /// for rules the toggles can't express.
    [CreateAssetMenu(menuName = "SlidingSiege/Combat Rules")]
    public class CombatRules : ScriptableObject
    {
        [Header("Toggles")]
        [Tooltip("Bomb-priority rule: a direct attack hit destroys this enemy outright and voids the rest of the attack.")]
        [SerializeField] private bool voidsAttackOnHit;
        [Tooltip("Golem rule: damage aimed at any enemy this one links is absorbed by this enemy instead (recomputed against ITS stats and statuses).")]
        [SerializeField] private bool absorbsLinkedDamage;
        [Tooltip("On (default): dropping to 0 HP kills this enemy immediately. Off: it goes critical instead (PendingDetonation, links dropped, OnCritical abilities fire) and survives until something removes it.")]
        [SerializeField] private bool diesAtZeroHP = true;
        [Tooltip("Off = link abilities (Golem/Siren) never pick this enemy as a target (e.g. Bomb).")]
        [SerializeField] private bool canBeLinkTarget = true;

        public bool VoidsAttackOnHit => voidsAttackOnHit;
        public bool AbsorbsLinkedDamage => absorbsLinkedDamage;
        public bool DiesAtZeroHP => diesAtZeroHP;
        public bool CanBeLinkTarget => canBeLinkTarget;

        private static CombatRules _default;
        /// Rules for definitions that assign none: plain enemy behavior.
        public static CombatRules Default =>
            _default != null ? _default : _default = CreateInstance<CombatRules>();

        /// The enemy that actually receives damage aimed at `target`: the
        /// first living, non-critical absorber linking it, or the target
        /// itself. Absorbers never redirect to one another.
        public virtual Enemy RouteDamage(GridState s, Enemy target)
        {
            if (!target.Rules.AbsorbsLinkedDamage)
                foreach (var en in s.Enemies.Values)
                    if (en.Rules.AbsorbsLinkedDamage && !en.PendingDetonation && en.IsLinkedTo(target.Id))
                        return en;
            return target;
        }

        /// Enemies that survive at 0 HP never drop below it (avoids a
        /// phantom heal indicator when clamping back up).
        public virtual int ClampDamage(Enemy en, int dmg) =>
            diesAtZeroHP ? dmg : Mathf.Min(dmg, en.HP);

        /// Called when the enemy is at 0 HP or less. Non-dying enemies go
        /// critical (pending detonation, links dropped, OnEnemyWentCritical
        /// raised) and survive — returns false; returns true on real death.
        public virtual bool HandleZeroHp(GridState s, Enemy en)
        {
            if (diesAtZeroHP) return true;
            if (!en.PendingDetonation)
            {
                en.GoCritical();
                Debug.Log($"[SlidingSiege] {en.Definition.name} is critically damaged and will explode next enemy phase!");
                s.NotifyEnemyWentCritical(en);
            }
            return false;
        }
    }
}
