using UnityEngine;

namespace Operator.Depth.Core
{
    public enum MagneticDirection
    {
        Down,
        Right,
        Up,
        Left
    }

    public enum MagneticMoveEffect
    {
        None,
        Boost,
        Slow,
        Against
    }

    public sealed class MagneticAnomaly
    {
        public int BlockX { get; }
        public int BlockY { get; }
        public Vector2Int Anchor { get; }
        public float RadiusX { get; }
        public float RadiusY { get; }
        public float RoamRadius { get; }
        public MagneticDirection Direction { get; set; }
        public int ShapeSeed { get; }
        public float BoostMultiplier { get; }
        public float SlowMultiplier { get; }
        public float AgainstMultiplier { get; }
        public Vector2 Center { get; set; }
        public Vector2 DriftVelocity { get; set; }
        public float DirectionTimer { get; set; }
        public int DirectionStep { get; set; }

        public MagneticAnomaly(
            int blockX,
            int blockY,
            Vector2Int anchor,
            Vector2 center,
            float radiusX,
            float radiusY,
            float roamRadius,
            MagneticDirection direction,
            int shapeSeed,
            Vector2 driftVelocity,
            float directionTimer,
            float boostMultiplier,
            float slowMultiplier,
            float againstMultiplier)
        {
            BlockX = blockX;
            BlockY = blockY;
            Anchor = anchor;
            Center = center;
            RadiusX = radiusX;
            RadiusY = radiusY;
            RoamRadius = roamRadius;
            Direction = direction;
            ShapeSeed = shapeSeed;
            DriftVelocity = driftVelocity;
            DirectionTimer = directionTimer;
            BoostMultiplier = boostMultiplier;
            SlowMultiplier = slowMultiplier;
            AgainstMultiplier = againstMultiplier;
        }

        public long Id => MagneticAnomalyIds.Key(BlockX, BlockY);

        public Vector3 CenterWorld => new(Center.x, -Center.y, 0f);

        public void GetBounds(out int xMin, out int xMax, out int yMin, out int yMax)
        {
            var padX = Mathf.CeilToInt(RadiusX + RoamRadius);
            var padY = Mathf.CeilToInt(RadiusY + RoamRadius);
            var anchorCellX = Mathf.FloorToInt(Center.x - WorldGrid.HalfCell);
            var anchorCellY = Mathf.FloorToInt(Center.y - WorldGrid.HalfCell);
            xMin = anchorCellX - padX;
            xMax = anchorCellX + padX;
            yMin = anchorCellY - padY;
            yMax = anchorCellY + padY;
        }

        public static float DirectionToFacingAngle(MagneticDirection direction) =>
            direction switch
            {
                MagneticDirection.Down => 0f,
                MagneticDirection.Right => 90f,
                MagneticDirection.Up => 180f,
                MagneticDirection.Left => -90f,
                _ => 0f
            };
    }

    public static class MagneticAnomalyIds
    {
        public static long Key(int blockX, int blockY) => ((long)blockX << 32) ^ (uint)blockY;
    }
}
