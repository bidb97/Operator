using Operator.Depth.Core;
using UnityEngine;

namespace Operator.Missions.Core
{
    /// <summary>Активная миссия текущей игры (save).</summary>
    public class ActiveMission
    {
        public string TemplateId;
        public MissionType Type;
        public SectorAddress TargetSector;
        public int TargetCellX;
        public int TargetCellY;

        public ResourceType MineResource;
        public int MineClusterCount;
        public string SpeakerTitle;
        public Sprite SpeakerAvatar;
        public string BriefingText;

        public Vector2Int TargetCell => new(TargetCellX, TargetCellY);

        public string TargetSectorDisplay => TargetSector.ToDisplayString();
    }
}
