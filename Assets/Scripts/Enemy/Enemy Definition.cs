using UnityEngine;

namespace SlidingSiege
{
    /// Authoring data for an enemy type. Size is (rows, cols).
    [CreateAssetMenu(menuName = "SlidingSiege/Enemy Definition")]
    public class EnemyDefinition : ScriptableObject
    {
        public Sprite Sprite;
        [Min(1)] public int SizeRows = 1;
        [Min(1)] public int SizeCols = 1;
    }
}
