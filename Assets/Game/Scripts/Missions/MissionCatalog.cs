using UnityEngine;

namespace Operator.Missions
{
    /// <summary>Упорядоченная цепочка миссий (Act 1 и далее).</summary>
    [CreateAssetMenu(fileName = "MissionCatalog", menuName = "Operator/Mission Catalog")]
    public class MissionCatalog : ScriptableObject
    {
        [SerializeField] MissionTemplate[] missions;

        public int Count => missions != null ? missions.Length : 0;

        public MissionTemplate Get(int index)
        {
            if (missions == null || index < 0 || index >= missions.Length)
            {
                return null;
            }

            return missions[index];
        }
    }
}
