using UnityEngine;
using UnityEngine.UI;

namespace SlidingSiege
{
    /// Root component of the enemy piece prefab:
    ///   Root (EnemyPieceView + CanvasGroup) — sized to the footprint
    ///     ├─ Sprite Image (stretch-anchored to fill the root)
    ///     └─ Health Bar (EnemyHealthBarDisplay; sibling of the sprite)
    /// Every piece (main + wrap ghosts) has its own health bar; they all
    /// bind to the same enemy so they display the same health.
    [RequireComponent(typeof(CanvasGroup))]
    public class EnemyPieceView : MonoBehaviour
    {
        [SerializeField] private Image spriteImage;
        [SerializeField] private EnemyHealthBarDisplay healthBar;

        private CanvasGroup _canvasGroup;

        public Image SpriteImage => spriteImage;
        public EnemyHealthBarDisplay HealthBar => healthBar;
        public RectTransform RectTransform => (RectTransform)transform;
        public CanvasGroup CanvasGroup
        {
            get
            {
                if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
                return _canvasGroup;
            }
        }
    }
}
