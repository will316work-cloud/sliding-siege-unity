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

        [Header("Animation")]
        [Tooltip("Replaces the enemy piece prefab's Animator controller for this enemy; empty keeps the prefab default. State names must match the prefab's AnimationCaller presets (Enemy Spawn/Move/Hurt/Death/Idle).")]
        [SerializeField] private RuntimeAnimatorController animatorController;
        [SerializeField] private TimedAnimationPreset hurtPreset = new TimedAnimationPreset();
        [SerializeField] private TimedAnimationPreset deathPreset = new TimedAnimationPreset();
        [SerializeField] private TimedAnimationPreset idlePreset = new TimedAnimationPreset();
        [Tooltip("Looping label played instead of Idle while the enemy is critical (pending detonation). Empty falls back to Idle.")]
        [SerializeField] private TimedAnimationPreset criticalPreset = new TimedAnimationPreset();

        [Header("Enemy remains")]
        [Tooltip("Multiplied over every color of the Enemy Remains particle system.")]
        [SerializeField] private Color remainsColorOverlay = Color.white;
        [Tooltip("Multiplied over every color of the Explosion Flakes particle system.")]
        [SerializeField] private Color explosionFlakesColorOverlay = Color.white;
        [Tooltip("Multiplied over every color of the Explosion Cloud particle system.")]
        [SerializeField] private Color explosionCloudColorOverlay = Color.white;

        public IReadOnlyList<EnemyAbility> Abilities => abilities;
        public int MaxHP => maxHP;
        public CombatRules Rules => combatRules != null ? combatRules : CombatRules.Default;
        public LinkDisplaySettings LinkDisplay => linkDisplay;
        public ImageSettings DisabledLineDisplay => disabledLineDisplay;
        public EnemyShape Shape => shape;
        public RuntimeAnimatorController AnimatorController => animatorController;
        public TimedAnimationPreset HurtPreset => hurtPreset;
        public TimedAnimationPreset DeathPreset => deathPreset;
        public TimedAnimationPreset IdlePreset => idlePreset;
        public TimedAnimationPreset CriticalPreset => criticalPreset;

        /// The preset this enemy should rest on right now: the critical
        /// preset while it is pending detonation (when one is labeled),
        /// otherwise Idle.
        public TimedAnimationPreset RestingPresetFor(Enemy en) =>
            en != null && en.PendingDetonation && !string.IsNullOrEmpty(criticalPreset.PresetLabel)
                ? criticalPreset : idlePreset;
        public Color RemainsColorOverlay => remainsColorOverlay;
        public Color ExplosionFlakesColorOverlay => explosionFlakesColorOverlay;
        public Color ExplosionCloudColorOverlay => explosionCloudColorOverlay;
    }
}
