using UnityEngine;

namespace Operator.Depth.Core
{
    public static class MagneticFieldQuery
    {
        const int ShapeNoiseSalt = unchecked((int)0x3C6EF372u);

        public static bool IsInside(MagneticAnomaly anomaly, int x, int y, int worldRadius)
        {
            if (anomaly == null || worldRadius <= 0)
            {
                return false;
            }

            if (y < 0 || y > worldRadius || GarageBounds.Contains(x, y))
            {
                return false;
            }

            anomaly.GetBounds(out var xMin, out var xMax, out var yMin, out var yMax);
            if (x < xMin || x > xMax || y < yMin || y > yMax)
            {
                return false;
            }

            var centerCellX = Mathf.FloorToInt(anomaly.Center.x - WorldGrid.HalfCell);
            var centerCellY = Mathf.FloorToInt(anomaly.Center.y - WorldGrid.HalfCell);
            return IsCellInBlob(
                x,
                y,
                centerCellX,
                centerCellY,
                anomaly.ShapeSeed,
                anomaly.RadiusX,
                anomaly.RadiusY);
        }

        /// <summary>Проверка по позиции дрона — гладкий эллипс, без скачков на границе клетки.</summary>
        public static bool IsInsideWorld(MagneticAnomaly anomaly, Vector3 worldPos, int worldRadius)
        {
            if (anomaly == null || worldRadius <= 0)
            {
                return false;
            }

            var cellX = Mathf.FloorToInt(worldPos.x);
            var cellY = Mathf.FloorToInt(-worldPos.y);
            if (cellY < 0 || cellY > worldRadius || GarageBounds.Contains(cellX, cellY))
            {
                return false;
            }

            if (anomaly.RadiusX <= 0f || anomaly.RadiusY <= 0f)
            {
                return false;
            }

            var lx = worldPos.x - anomaly.Center.x;
            var ly = -worldPos.y - anomaly.Center.y;
            var nx = lx / anomaly.RadiusX;
            var ny = ly / anomaly.RadiusY;
            return nx * nx + ny * ny < 1f;
        }

        public static MagneticMoveEffect GetMoveEffect(MagneticAnomaly anomaly, float facingAngleDegrees)
        {
            if (anomaly == null)
            {
                return MagneticMoveEffect.None;
            }

            var fieldAngle = MagneticAnomaly.DirectionToFacingAngle(anomaly.Direction);
            var delta = Mathf.Abs(Mathf.DeltaAngle(facingAngleDegrees, fieldAngle));

            if (delta < 0.01f)
            {
                return MagneticMoveEffect.Boost;
            }

            if (delta > 179.99f)
            {
                return MagneticMoveEffect.Against;
            }

            if (delta > 89.99f && delta < 90.01f)
            {
                return MagneticMoveEffect.Slow;
            }

            return MagneticMoveEffect.None;
        }

        public static float GetSpeedMultiplier(MagneticAnomaly anomaly, MagneticMoveEffect effect) =>
            effect switch
            {
                MagneticMoveEffect.Boost => anomaly.BoostMultiplier,
                MagneticMoveEffect.Slow => anomaly.SlowMultiplier,
                MagneticMoveEffect.Against => anomaly.AgainstMultiplier,
                _ => 1f
            };

        static bool IsCellInBlob(
            int x,
            int y,
            int anchorX,
            int anchorY,
            int shapeSeed,
            float radiusX,
            float radiusY)
        {
            if (radiusX <= 0f || radiusY <= 0f)
            {
                return false;
            }

            var lx = x - anchorX;
            var ly = y - anchorY;
            var nx = lx / radiusX;
            var ny = ly / radiusY;
            var dist = nx * nx + ny * ny;
            var noise = Hash01(shapeSeed, ShapeNoiseSalt, lx, ly);
            noise = (noise - 0.5f) * 0.55f;
            return dist + noise < 1f;
        }

        static float Hash01(int shapeSeed, int salt, int lx, int ly)
        {
            unchecked
            {
                uint h = (uint)shapeSeed;
                h ^= (uint)salt;
                h ^= (uint)(lx + 17) * 0xC2B2AE35u;
                h ^= (uint)(ly + 31) * 0x27D4EB2Fu;
                h = ((h >> 16) ^ h) * 0x7FEB352Du;
                h = ((h >> 15) ^ h) * 0x846CA68Bu;
                h = (h >> 16) ^ h;
                return h / (float)uint.MaxValue;
            }
        }
    }
}
