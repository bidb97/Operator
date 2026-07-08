using Operator.Depth.Core;
using Operator.Missions.Core;
using UnityEngine;

namespace Operator.Missions
{
    [CreateAssetMenu(fileName = "MissionTemplate", menuName = "Operator/Mission Template")]
    public class MissionTemplate : ScriptableObject
    {
        [Header("Идентификация")]
        [Tooltip("Уникальный ID шаблона. Если пусто — берётся имя ассета.")]
        [SerializeField] string templateId;

        [Header("Тип")]
        [Tooltip("Тип миссии. От него зависит, какие поля ниже нужны.")]
        [SerializeField] MissionType missionType = MissionType.InstallScanner;

        [Header("Сектор (roll из seed)")]
        [Tooltip("D сектора: min…max. 0 = у поверхности.")]
        [SerializeField] int depthMin;
        [SerializeField] int depthMax = 2;
        [Tooltip("|R| от ствола: 0 = под шахтой, 1 = один сектор вбок… L/R — roll из seed.")]
        [SerializeField] int lateralMin;
        [SerializeField] int lateralMax = 2;

        [Header("Сюжет: фикс сектор")]
        [Tooltip("Если включено — depth/lateral min-max игнорируются.")]
        [SerializeField] bool useFixedSector;
        [SerializeField] int fixedSectorDepth;
        [SerializeField] SectorLateralSide fixedLateralSide = SectorLateralSide.Shaft;
        [SerializeField] int fixedLateralDistance;

        [Header("Клетка-якорь")]
        [Tooltip("Roll в секторе или фикс (сюжет). SectorCenter не использовать.")]
        [SerializeField] MissionTargetCellMode targetCellMode = MissionTargetCellMode.RolledInSector;
        [SerializeField] int fixedCellX;
        [SerializeField] int fixedCellY;

        [Header("Добыча")]
        [Tooltip("Какой ресурс добывать. Только для типа MineClusters.")]
        [SerializeField] ResourceType mineResource = ResourceType.Synth;
        [Tooltip("Сколько кластеров добыть. Только для типа MineClusters.")]
        [SerializeField] int mineClusterCount = 2;

        [Header("Брифинг")]
        [SerializeField] string speakerTitle = "VIYA";
        [TextArea(3, 8)]
        [SerializeField] string briefingText =
            "Установи сканер ресурсов в секторе {sector}.";

        public string TemplateId => string.IsNullOrEmpty(templateId) ? name : templateId;
        public MissionType MissionType => missionType;
        public int DepthMin => depthMin;
        public int DepthMax => depthMax;
        public int LateralMin => lateralMin;
        public int LateralMax => lateralMax;
        public bool UseFixedSector => useFixedSector;
        public int FixedSectorDepth => fixedSectorDepth;
        public SectorLateralSide FixedLateralSide => fixedLateralSide;
        public int FixedLateralDistance => fixedLateralDistance;
        public MissionTargetCellMode TargetCellMode => targetCellMode;
        public int FixedCellX => fixedCellX;
        public int FixedCellY => fixedCellY;
        public ResourceType MineResource => mineResource;
        public int MineClusterCount => mineClusterCount;
        public string SpeakerTitle => speakerTitle;
        public string BriefingText => briefingText;

        public SectorAddress GetFixedSectorAddress() =>
            new(
                SectorAddressResolver.LateralBlock(fixedLateralSide, fixedLateralDistance),
                fixedSectorDepth);

        void OnValidate()
        {
            depthMin = Mathf.Max(0, depthMin);
            depthMax = Mathf.Max(depthMin, depthMax);
            lateralMin = Mathf.Max(0, lateralMin);
            lateralMax = Mathf.Max(lateralMin, lateralMax);
            fixedSectorDepth = Mathf.Max(0, fixedSectorDepth);
            fixedLateralDistance = Mathf.Max(0, fixedLateralDistance);
            mineClusterCount = Mathf.Max(1, mineClusterCount);
        }
    }
}
