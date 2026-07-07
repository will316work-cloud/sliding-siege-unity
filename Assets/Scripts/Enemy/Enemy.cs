using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SlidingSiege
{
    /// Runtime enemy instance. Anchor is the top-left cell of its footprint
    /// (always normalized into [0,rows) x [0,cols); the footprint may wrap).
    public class Enemy
    {
        public int Id;
        public EnemyDefinition Definition;
        public Vector2Int Anchor;          // (row, col) => (x = row, y = col)
        public int SizeRows => Definition.SizeRows;
        public int SizeCols => Definition.SizeCols;

        private int _hp;

        /// Current health. Setting it raises OnHealthChanged(current, max).
        public int HP
        {
            get => _hp;
            set
            {
                if (_hp == value) return;
                int delta = value - _hp;
                _hp = value;
                OnHealthChanged?.Invoke(_hp, MaxHP);
                if (delta < 0) OnHealthLost?.Invoke(-delta);
                else OnHealthGained?.Invoke(delta);
            }
        }

        public int MaxHP => Definition != null ? Definition.MaxHP : 0;

        /// (currentHP, maxHP) — raised whenever HP changes.
        public event Action<int, int> OnHealthChanged;

        /// Raised with the (positive) amount whenever HP decreases.
        public event Action<int> OnHealthLost;

        /// Raised with the (positive) amount whenever HP increases.
        public event Action<int> OnHealthGained;

        public bool IsDead => HP <= 0;
        public readonly List<StatusEffect> Statuses = new List<StatusEffect>();

        /// Product of all status damage-taken multipliers.
        public float DamageTakenMultiplier()
        {
            float m = 1f;
            foreach (var s in Statuses) m *= s.DamageTakenMultiplier;
            return m;
        }

        public bool HasStatus<T>() where T : StatusEffect => Statuses.OfType<T>().Any();

        /// False while dead or any status (e.g. a future stun) vetoes acting.
        public bool CanAct => !IsDead && Statuses.All(s => !s.PreventsAction);
    }
}
