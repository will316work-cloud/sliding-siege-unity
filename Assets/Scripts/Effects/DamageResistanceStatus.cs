using UnityEngine;

namespace SlidingSiege
{
    /// Sprite's ward: the enemy takes reduced damage for a limited number
    /// of enemy phases.
    public class DamageResistanceStatus : StatusEffect
    {
        private readonly float _multiplier;

        public DamageResistanceStatus(float reduction, int turns)
        {
            _multiplier = Mathf.Clamp01(1f - reduction);
            TurnsRemaining = turns;
        }

        public override float DamageTakenMultiplier => _multiplier;
    }
}
