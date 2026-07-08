namespace Operator.Missions.Core
{
    /// <summary>Как выбрать клетку установки / якорь цели внутри сектора.</summary>
    public enum MissionTargetCellMode
    {
        /// <summary>Центр сектора (block * size + size/2).</summary>
        SectorCenter = 0,

        /// <summary>Детерминированный roll из seed внутри сектора.</summary>
        RolledInSector = 1,

        /// <summary>Точные координаты из шаблона.</summary>
        FixedCell = 2
    }
}
