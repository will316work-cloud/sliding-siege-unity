using UnityEngine;

namespace SlidingSiege
{
    /// Shared authoring data for anything AbilityListUI can render as a
    /// card: attacks and items. AttackDefinition/ItemDefinition fill in
    /// their own divergent parts (damage/hitbox variants vs item kind/a
    /// single hitbox) through these overrides, so AbilityListUI and the
    /// card-selection flow can stay polymorphic instead of branching on
    /// concrete type.
    public abstract class AbilityDefinition : ScriptableObject
    {
        [SerializeField] private string displayName;
        [SerializeField] private Sprite icon;
        [SerializeField, Min(0)] private int startingCount = 2;
        [SerializeField, TextArea] private string description;

        public string DisplayName => displayName;
        public Sprite Icon => icon;
        public int StartingCount => startingCount;
        public string Description => description;

        /// Registers this ability's starting count with combat. Identical
        /// for every concrete type (CombatSystem keys its inventory by the
        /// AbilityDefinition instance) — not virtual.
        public void Setup(CombatSystem combat) => combat.Setup(this);
        /// Remaining charges/count for this ability right now. As Setup,
        /// identical for every concrete type — not virtual.
        public int GetCount(CombatSystem combat) => combat.GetCount(this);
        /// True if uses/count never deplete for this ability (CombatSystem's
        /// InfiniteAttacks/InfiniteItems debug toggle, per concrete type —
        /// the one thing that genuinely differs at the CombatSystem level).
        public abstract bool IsInfinite(CombatSystem combat);
        /// Whether this ability can be used right now.
        public abstract bool CanUse(CombatSystem combat);
        /// Card damage label, or null to hide it (e.g. items have none).
        public virtual string DamageLabel(CombatSystem combat) => null;
        /// Confirm-button label while this is the active selection.
        /// `variantIndex` is ignored by types with no variants (items).
        public virtual string ConfirmLabel(int variantIndex) => DisplayName;
    }
}
