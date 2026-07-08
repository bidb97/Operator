using Operator.Depth.Core;
using Operator.Missions;
using UnityEngine;

namespace Operator.Missions.Core
{
    public readonly struct ResolvedMission
    {
        public MissionTemplate Template { get; }
        public SectorAddress TargetSector { get; }
        public Vector2Int TargetCell { get; }

        public ResolvedMission(MissionTemplate template, SectorAddress targetSector, Vector2Int targetCell)
        {
            Template = template;
            TargetSector = targetSector;
            TargetCell = targetCell;
        }
    }

    /// <summary>Из шаблона + seed → сектор и клетка-якорь (подсветка / установка).</summary>
    public static class MissionResolver
    {
        const int CellRollSalt = unchecked((int)0xC2B2AE35u);
        const int SectorRollSalt = unchecked((int)0x7F4A7C15u);

        public static ResolvedMission Resolve(
            MissionTemplate template,
            int worldSeed,
            int sectorSize,
            int missionIndex = 0,
            int attempt = 0)
        {
            if (template == null || sectorSize <= 0)
            {
                return default;
            }

            var sector = RollSector(template, worldSeed, missionIndex, attempt);
            var cell = ResolveTargetCell(template, sector, worldSeed, sectorSize, missionIndex, attempt);
            return new ResolvedMission(template, sector, cell);
        }

        public static SectorAddress RollSector(
            MissionTemplate template,
            int worldSeed,
            int missionIndex,
            int attempt = 0)
        {
            if (template.UseFixedSector)
            {
                return template.GetFixedSectorAddress();
            }

            var templateSalt = StableHash(template.TemplateId);

            var depthSpan = template.DepthMax - template.DepthMin + 1;
            var depthU = Hash01(worldSeed, missionIndex, templateSalt, SectorRollSalt, 0, attempt);
            var depth = template.DepthMin + Mathf.FloorToInt(depthU * depthSpan);

            var latSpan = template.LateralMax - template.LateralMin + 1;
            var latU = Hash01(worldSeed, missionIndex, templateSalt, SectorRollSalt, 1, attempt);
            var latMag = template.LateralMin + Mathf.FloorToInt(latU * latSpan);

            if (latMag == 0)
            {
                return new SectorAddress(0, depth);
            }

            var signU = Hash01(worldSeed, missionIndex, templateSalt, SectorRollSalt, 2, attempt);
            var blockX = signU < 0.5f ? -latMag : latMag;
            return new SectorAddress(blockX, depth);
        }

        public static Vector2Int RollTargetCell(
            MissionTemplate template,
            SectorAddress sector,
            int worldSeed,
            int sectorSize,
            int missionIndex,
            int attempt)
        {
            return ResolveTargetCell(template, sector, worldSeed, sectorSize, missionIndex, attempt);
        }

        public static ActiveMission ToActiveMission(ResolvedMission resolved)
        {
            if (resolved.Template == null)
            {
                return null;
            }

            var template = resolved.Template;
            return new ActiveMission
            {
                TemplateId = template.TemplateId,
                Type = template.MissionType,
                TargetSector = resolved.TargetSector,
                TargetCellX = resolved.TargetCell.x,
                TargetCellY = resolved.TargetCell.y,
                MineResource = template.MineResource,
                MineClusterCount = template.MineClusterCount,
                SpeakerTitle = template.SpeakerTitle,
                BriefingText = FormatBriefingText(template, resolved)
            };
        }

        public static string FormatBriefingText(MissionTemplate template, ResolvedMission resolved)
        {
            if (template == null)
            {
                return string.Empty;
            }

            var text = template.BriefingText ?? string.Empty;
            text = text.Replace("{sector}", resolved.TargetSector.ToDisplayString());
            text = text.Replace("{resource}", ResourceLabel(template.MineResource));
            text = text.Replace("{amount}", template.MineClusterCount.ToString());
            return text;
        }

        static Vector2Int ResolveTargetCell(
            MissionTemplate template,
            SectorAddress sector,
            int worldSeed,
            int sectorSize,
            int missionIndex,
            int attempt)
        {
            return template.TargetCellMode switch
            {
                MissionTargetCellMode.FixedCell => new Vector2Int(template.FixedCellX, template.FixedCellY),
                MissionTargetCellMode.RolledInSector => RollCellInSector(
                    sector,
                    worldSeed,
                    template.TemplateId,
                    sectorSize,
                    missionIndex,
                    attempt),
                _ => SectorCenterCell(sector, sectorSize)
            };
        }

        public static Vector2Int SectorCenterCell(SectorAddress sector, int sectorSize)
        {
            sector.GetCellRect(sectorSize, out var xMin, out _, out var yMin, out _);
            var half = sectorSize / 2;
            return new Vector2Int(xMin + half, yMin + half);
        }

        static Vector2Int RollCellInSector(
            SectorAddress sector,
            int worldSeed,
            string templateId,
            int sectorSize,
            int missionIndex,
            int attempt)
        {
            sector.GetCellRect(sectorSize, out var xMin, out var xMax, out var yMin, out var yMax);
            var templateSalt = StableHash(templateId);

            var u = Hash01(worldSeed, sector.BlockX, sector.BlockY, CellRollSalt, templateSalt, attempt);
            var v = Hash01(worldSeed, sector.BlockX, sector.BlockY, CellRollSalt, templateSalt, attempt + 1000);

            var localX = Mathf.FloorToInt(u * sectorSize);
            var localY = Mathf.FloorToInt(v * sectorSize);
            var x = xMin + localX;
            var y = yMin + localY;

            if (IsValidMissionCell(x, y))
            {
                return new Vector2Int(x, y);
            }

            return SectorCenterCell(sector, sectorSize);
        }

        static bool IsValidMissionCell(int x, int y) =>
            y >= 1 && !GarageBounds.Contains(x, y);

        static string ResourceLabel(ResourceType type) =>
            type switch
            {
                ResourceType.Synth => "Синтет",
                ResourceType.Rellit => "Реллит",
                ResourceType.Lumin => "Люмин",
                ResourceType.DeVault => "Де-ваульт",
                _ => "—"
            };

        static int StableHash(string text)
        {
            unchecked
            {
                var hash = 17;
                foreach (var c in text)
                {
                    hash = hash * 31 + c;
                }

                return hash;
            }
        }

        static float Hash01(
            int worldSeed,
            int blockX,
            int blockY,
            int salt,
            int extraSalt,
            int attempt)
        {
            unchecked
            {
                uint h = (uint)worldSeed;
                h ^= (uint)blockX * 0x9E3779B9u;
                h ^= (uint)blockY * 0x85EBCA6Bu;
                h ^= (uint)salt;
                h ^= (uint)extraSalt * 0xC2B2AE35u;
                h ^= (uint)attempt * 0x27D4EB2Fu;
                h = (h ^ (h >> 16)) * 0x85EBCA6Bu;
                h = (h ^ (h >> 13)) * 0xC2B2AE35u;
                h ^= h >> 16;
                return h / (float)uint.MaxValue;
            }
        }
    }
}
