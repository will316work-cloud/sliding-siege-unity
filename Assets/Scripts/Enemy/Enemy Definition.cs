using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace SlidingSiege
{
    /// Authoring data for an enemy type. The body cells, sprite, and visual
    /// rect live in the embedded EnemyShape (the same structure runtime
    /// shape overrides use); Image settings stay here and apply regardless
    /// of the active shape. Combat behavior (bomb void, golem absorb,
    /// survives-at-zero, link targeting) lives in the referenced
    /// CombatRules strategy asset.
    [CreateAssetMenu(menuName = "SlidingSiege/Enemy Definition")]
    public class EnemyDefinition : ScriptableObject
    {
        [Header("Abilities")]
        [Tooltip("Ability assets this enemy runs; EnemyPhase ones execute by Order Index during the enemy phase, other triggers run event-driven (see AbilityTrigger).")]
        [FormerlySerializedAs("Abilities")]
        [SerializeField] private List<EnemyAbility> abilities = new List<EnemyAbility>();

        [Header("Stats")]
        [FormerlySerializedAs("MaxHP")]
        [SerializeField, Min(1)] private int maxHP = 30;

        [Header("Combat rules")]
        [Tooltip("Strategy asset with this enemy's combat rules (bomb void, golem absorb, dies-at-zero, link targeting). Empty = default rules.")]
        [SerializeField] private CombatRules combatRules;

        [Header("Linking display")]
        [Tooltip("How LinkOverlay draws this enemy's link lines and the redirected-hit pulse on its linked targets.")]
        [FormerlySerializedAs("LinkDisplay")]
        [SerializeField] private LinkDisplaySettings linkDisplay = new LinkDisplaySettings();

        [Header("Disabled line display")]
        [Tooltip("How DisabledLineOverlay draws the rows/columns this enemy's curses disable.")]
        [SerializeField] private DisabledLineDisplaySettings disabledLineDisplay = new DisabledLineDisplaySettings();

        [Header("Base shape (body, sprite, visual rect)")]
        [FormerlySerializedAs("Shape")]
        [SerializeField] private EnemyShape shape = new EnemyShape();

        [Header("Image settings")]
        [FormerlySerializedAs("ImageType")]
        [SerializeField] private Image.Type imageType = Image.Type.Simple;
        [Tooltip("Simple/Filled only.")]
        [FormerlySerializedAs("PreserveAspect")]
        [SerializeField] private bool preserveAspect = false;
        [Tooltip("Sliced/Tiled only.")]
        [FormerlySerializedAs("PixelsPerUnitMultiplier")]
        [SerializeField] private float pixelsPerUnitMultiplier = 1f;
        [Tooltip("Sliced only: draw the center region.")]
        [FormerlySerializedAs("FillCenter")]
        [SerializeField] private bool fillCenter = true;
        [Tooltip("Filled only.")]
        [FormerlySerializedAs("FillMethod")]
        [SerializeField] private Image.FillMethod fillMethod = Image.FillMethod.Radial360;
        [Tooltip("Filled only."), Range(0f, 1f)]
        [FormerlySerializedAs("FillAmount")]
        [SerializeField] private float fillAmount = 1f;
        [Tooltip("Color overlay / tint multiplied over the sprite.")]
        [FormerlySerializedAs("ColorOverlay")]
        [SerializeField] private Color colorOverlay = Color.white;
        [FormerlySerializedAs("Material")]
        [SerializeField] private Material material;
        [FormerlySerializedAs("RaycastTarget")]
        [SerializeField] private bool raycastTarget = false;

        public IReadOnlyList<EnemyAbility> Abilities => abilities;
        public int MaxHP => maxHP;
        public CombatRules Rules => combatRules != null ? combatRules : CombatRules.Default;
        public LinkDisplaySettings LinkDisplay => linkDisplay;
        public DisabledLineDisplaySettings DisabledLineDisplay => disabledLineDisplay;
        public EnemyShape Shape => shape;
        public Color ColorOverlay => colorOverlay;

        /// Applies all Image settings to a piece. The sprite comes from the
        /// active shape (see Enemy.CurrentSprite); size/position are handled
        /// by the view layer (they depend on grid metrics).
        public void ApplyTo(Image img)
        {
            img.type = imageType;
            img.preserveAspect = preserveAspect;
            img.pixelsPerUnitMultiplier = pixelsPerUnitMultiplier;
            img.fillCenter = fillCenter;
            img.fillMethod = fillMethod;
            img.fillAmount = fillAmount;
            img.color = colorOverlay;
            img.material = material;
            img.raycastTarget = raycastTarget;
        }
    }
}
