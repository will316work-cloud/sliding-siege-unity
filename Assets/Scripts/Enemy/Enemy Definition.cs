using System.Collections.Generic;
using UnityEngine;

namespace SlidingSiege
{
    /// Authoring data for an enemy type. The body cells, Image settings
    /// (sprite, tint, fills...), and visual rect live in the embedded
    /// EnemyShape (the same structure runtime shape overrides use).
    /// Combat behavior (bomb void, golem absorb,
    /// survives-at-zero, link targeting) lives in the referenced
    /// CombatRules strategy asset.
    [CreateAssetMenu(menuName = "SlidingSiege/Enemy Definition")]
    public class EnemyDefinition : ScriptableObject
    {
        [Header("Abilities")]
        [Tooltip("Ability assets this enemy runs; EnemyPhase ones execute by Order Index during the enemy phase, other triggers run event-driven (see AbilityTrigger).")]
        [SerializeField] private List<EnemyAbility> abilities = new List<EnemyAbility>();

        [Header("Stats")]
        [SerializeField, Min(1)] private int maxHP = 30;

        [Header("Combat rules")]
        [Tooltip("Strategy asset with this enemy's combat rules (bomb void, golem absorb, dies-at-zero, link targeting). Empty = default rules.")]
        [SerializeField] private CombatRules combatRules;

        [Header("Linking display")]
        [Tooltip("How LinkOverlay draws this enemy's link lines and the redirected-hit pulse on its linked targets.")]
        [SerializeField] private LinkDisplaySettings linkDisplay = new LinkDisplaySettings();

        [Header("Disabled line display")]
        [Tooltip("How DisabledLineOverlay draws the rows/columns this enemy's curses disable.")]
        [SerializeField] private ImageSettings disabledLineDisplay = new ImageSettings();

        [Header("Base shape (body, Image settings, visual rect)")]
        [SerializeField] private EnemyShape shape = new EnemyShape();

        public IReadOnlyList<EnemyAbility> Abilities => abilities;
        public int MaxHP => maxHP;
        public CombatRules Rules => combatRules != null ? combatRules : CombatRules.Default;
        public LinkDisplaySettings LinkDisplay => linkDisplay;
        public ImageSettings DisabledLineDisplay => disabledLineDisplay;
        public EnemyShape Shape => shape;
    }
}
