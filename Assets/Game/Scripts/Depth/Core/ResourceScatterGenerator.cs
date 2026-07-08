using Operator.Data;

namespace Operator.Depth.Core
{
    /// <summary>
    /// Одиночные ресурсы Synth/Rellit/Lumin. De-Vault только в пачках.
    /// </summary>
    public static class ResourceScatterGenerator
    {
        const int ScatterSalt = unchecked((int)0xC6A4A793u);
        const int TypeSalt = unchecked((int)0x5BD1E995u);

        public static bool TryGetResource(
            int x,
            int y,
            int worldSeed,
            int worldRadius,
            WorldGenConfig config,
            out ResourceType resource)
        {
            resource = ResourceType.None;

            if (config == null || worldRadius <= 0 || config.ResourceScatterChance <= 0f)
            {
                return false;
            }

            if (!GarageBounds.CanHostResource(x, y, worldRadius))
            {
                return false;
            }

            if (Hash01(worldSeed, x, y, ScatterSalt) >= config.ResourceScatterChance)
            {
                return false;
            }

            var pick = (int)(Hash01(worldSeed, x, y, TypeSalt) * 3f);
            resource = pick switch
            {
                0 => ResourceType.Synth,
                1 => ResourceType.Rellit,
                _ => ResourceType.Lumin
            };

            return true;
        }

        static float Hash01(int worldSeed, int x, int y, int salt)
        {
            unchecked
            {
                uint h = (uint)worldSeed;
                h ^= (uint)x * 0x9E3779B9u;
                h ^= (uint)y * 0x85EBCA6Bu;
                h ^= (uint)salt;
                h = ((h >> 16) ^ h) * 0x7FEB352Du;
                h = ((h >> 15) ^ h) * 0x846CA68Bu;
                h = (h >> 16) ^ h;
                return h / (float)uint.MaxValue;
            }
        }
    }
}
