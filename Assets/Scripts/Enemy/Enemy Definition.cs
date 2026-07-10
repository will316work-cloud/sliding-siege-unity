using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SlidingSiege
{
    /// Authoring data for an enemy type. The body cells, sprite, and visual
    /// rect live in the embedded EnemyShape (the same structure runtime
    /// shape overrides use); Image settings stay here and apply regardless
    /// of the active shape.
    [CreateAssetMenu(menuName = "SlidingSiege/Enemy Definition")]
    public class EnemyDefinition : ScriptableObject
    {
        [Header("Abilities")]
        [Tooltip("Ability assets this enemy runs during the enemy phase, ordered by each ability's Order Index (see EnemyAbility).")]
        public List<EnemyAbility> Abilities = new List<EnemyAbility>();

        [Header("Stats")]
        [Min(1)] public int MaxHP = 30;

        [Header("Combat rules")]
        [Tooltip("Bomb-priority rule: a direct attack hit destroys this enemy outright and voids the rest of the attack — no other enemy takes damage from it.")]
        public bool VoidsAttackOnHit;

        [Tooltip("Golem rule: damage aimed at any enemy this one links is absorbed by this enemy instead (recomputed against ITS stats and statuses).")]
        public bool AbsorbsLinkedDamage;

        [Tooltip("Golem rule: at 0 HP this enemy goes critical (Enemy.PendingDetonation) instead of dying — its own condition-gated abilities handle the detonation next enemy phase.")]
        public bool DetonatesAtZeroHP;

        [Tooltip("False = link abilities (Golem/Siren) can never pick this enemy as a link target (e.g. Bomb).")]
        public bool CanBeLinkTarget = true;

        [Header("Linking display")]
        [Tooltip("How LinkOverlay draws this enemy's link lines and the redirected-hit pulse on its linked targets.")]
        public LinkDisplaySettings LinkDisplay = new LinkDisplaySettings();

        [Header("Base shape (body, sprite, visual rect)")]
        public EnemyShape Shape = new EnemyShape();

        [Header("Image settings")]
        public Image.Type ImageType = Image.Type.Simple;
        [Tooltip("Simple/Filled only.")]
        public bool PreserveAspect = false;
        [Tooltip("Sliced/Tiled only.")]
        public float PixelsPerUnitMultiplier = 1f;
        [Tooltip("Sliced only: draw the center region.")]
        public bool FillCenter = true;
        [Tooltip("Filled only.")]
        public Image.FillMethod FillMethod = Image.FillMethod.Radial360;
        [Tooltip("Filled only."), Range(0f, 1f)]
        public float FillAmount = 1f;
        [Tooltip("Color overlay / tint multiplied over the sprite.")]
        public Color ColorOverlay = Color.white;
        public Material Material;
        public bool RaycastTarget = false;

        /// Applies all Image settings to a piece. The sprite comes from the
        /// active shape (see Enemy.CurrentSprite); size/position are handled
        /// by the view layer (they depend on grid metrics).
        public void ApplyTo(Image img)
        {
            img.type = ImageType;
            img.preserveAspect = PreserveAspect;
            img.pixelsPerUnitMultiplier = PixelsPerUnitMultiplier;
            img.fillCenter = FillCenter;
            img.fillMethod = FillMethod;
            img.fillAmount = FillAmount;
            img.color = ColorOverlay;
            img.material = Material;
            img.raycastTarget = RaycastTarget;
        }
    }
}
