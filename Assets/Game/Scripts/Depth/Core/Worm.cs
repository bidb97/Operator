using System.Collections.Generic;
using UnityEngine;

namespace Operator.Depth.Core
{
    public enum WormState
    {
        Dormant,
        Wander,
        Attack
    }

    public sealed class Worm
    {
        public int BlockX { get; }
        public int BlockY { get; }
        public int Slot { get; }
        public List<Vector2Int> Segments { get; }
        public List<Vector2Int> PreviousSegments { get; }
        public WormState State { get; set; }
        /// <summary>Заспавнен у жилы (иначе wild). Шанс атаки — при триггере, не при спавне.</summary>
        public bool OnVein { get; }
        /// <summary>Визуал / prefab: Synth, Rellit, Lumin, DeVault.</summary>
        public ResourceType Kind { get; }
        /// <summary>Игрок подошёл — симуляция не засыпает при уходе камеры.</summary>
        public bool Awake { get; set; }
        public int TargetLength { get; }
        public int SimStep { get; set; }
        public float MoveProgress { get; set; } = 1f;
        /// <summary>Оставшееся время окна атаки, сек.</summary>
        public float AttackTimer { get; set; }
        public float AttackCooldownTimer { get; set; }
        /// <summary>После отказа от атаки (кубик) — до следующей попытки.</summary>
        public float AttackRetryTimer { get; set; }
        /// <summary>Оставшиеся клетки рывка в текущей серии.</summary>
        public int AttackBurstStepsLeft { get; set; }
        /// <summary>Сек/клетку для текущего шага (атака — меняется).</summary>
        public float NextStepInterval { get; set; }

        public Worm(
            int blockX,
            int blockY,
            int slot,
            List<Vector2Int> segments,
            WormState state,
            int targetLength,
            bool onVein,
            ResourceType kind)
        {
            BlockX = blockX;
            BlockY = blockY;
            Slot = slot;
            Segments = segments;
            PreviousSegments = new List<Vector2Int>(segments);
            State = state;
            TargetLength = targetLength;
            OnVein = onVein;
            Kind = kind;
        }

        public long Id => WormIds.Key(BlockX, BlockY, Slot);
    }

    public static class WormIds
    {
        public static long Key(int blockX, int blockY, int slot)
        {
            unchecked
            {
                return ((long)blockX << 32) ^ ((long)(uint)blockY << 8) ^ (uint)slot;
            }
        }
    }
}
