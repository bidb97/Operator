namespace Operator.Depth.Core
{
    public enum ResourceType
    {
        None = 0,
        Synth = 1,
        Rellit = 2,
        Lumin = 3,
        DeVault = 4
    }

    public static class ResourceTypeIds
    {
        public static bool HasResource(ResourceType type) => type != ResourceType.None;

        public static string AssetId(ResourceType type)
        {
            return type switch
            {
                ResourceType.Synth => "synth",
                ResourceType.Rellit => "rellit",
                ResourceType.Lumin => "lumin",
                ResourceType.DeVault => "devault",
                _ => null
            };
        }
    }
}
