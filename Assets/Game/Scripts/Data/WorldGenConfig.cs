using UnityEngine;

namespace Operator.Data
{
    [CreateAssetMenu(fileName = "WorldGenConfig", menuName = "Operator/World Gen Config")]
    public class WorldGenConfig : ScriptableObject
    {
        [Header("Мир")]
        [Tooltip("Seed: превью и новая игра (если randomWorldSeed выключен).")]
        [SerializeField] int worldSeed = 42;

        [Tooltip("Только новая игра: случайный seed. Превью не использует.")]
        [SerializeField] bool randomWorldSeed = true;

        [Tooltip("R — радиус мира в клетках: X −R…+R (wrap), Y 0…R (ядро). Превью: ±R от камеры, R вниз.")]
        [SerializeField] int worldRadius = 10000;

        [Header("Слои пород (глубина, % от R)")]
        [Tooltip("Верхняя граница каждого слоя: Edge, Sediment, Scale… Halo. Последний — до ядра.")]
        [SerializeField] float[] layerDepthPercents =
        {
            8f, 15f, 25f, 55f, 68f, 80f, 92f, 100f
        };

        [Header("Волнистость границ слоёв")]
        [Tooltip("Сдвиг каждой границы вверх/вниз, % глубины (не клетки). У каждого слоя свой шум.")]
        [SerializeField] float layerBoundaryWarpPercent = 5f;

        [Tooltip("Длина волны по X (меньше — плавнее и длиннее).")]
        [SerializeField] float layerWarpScaleX = 0.012f;

        [Tooltip("Вариация по глубине Y — ломает одинаковый рисунок на всех слоях.")]
        [SerializeField] float layerWarpScaleY = 0.006f;

        [Header("Вкрапления (пачки жёсткой породы)")]
        [Tooltip("Шаг сетки якорей пачек, в клетках.")]
        [SerializeField] int inclusionClusterSpacing = 40;

        [SerializeField] int inclusionClusterSizeMin = 5;
        [SerializeField] int inclusionClusterSizeMax = 12;

        [Tooltip("Радиус blob (клетки). SizeMin/Max задают площадь, radius — clamp.")]
        [SerializeField] int inclusionClusterRadiusMin = 3;

        [SerializeField] int inclusionClusterRadiusMax = 8;

        [Tooltip("Вероятность пачки в блоке сетки (0…1).")]
        [SerializeField] float inclusionClusterChance = 0.45f;

        [Tooltip("Макс. уровень породы над доминиантом (+N к лазеру). Edge → +1…+N только.")]
        [SerializeField] int inclusionMaxTierDelta = 4;

        [Header("Ресурсы — пачки")]
        [Tooltip("Шаг сетки якорей пачек, в клетках.")]
        [SerializeField] int resourceClusterSpacing = 70;

        [SerializeField] int resourceClusterSizeMin = 3;
        [SerializeField] int resourceClusterSizeMax = 10;

        [Tooltip("Радиус зоны пачки (клетки).")]
        [SerializeField] int resourceClusterRadiusMin = 2;
        [SerializeField] int resourceClusterRadiusMax = 4;

        [Tooltip("Вероятность пачки в блоке сетки (0…1).")]
        [SerializeField] float resourceClusterChance = 0.45f;

        [Tooltip("De-Vault только глубже этого % от R (Mantle/Halo).")]
        [SerializeField] float resourceDeVaultMinDepthPercent = 75f;

        [Tooltip("Шанс De-Vault среди пачек на глубине (0…1). Остальное — Synth/Rellit/Lumin.")]
        [SerializeField] float resourceDeVaultClusterWeight = 0.12f;

        [Header("Ресурсы — одиночные (Synth/Rellit/Lumin)")]
        [Tooltip("Шанс одиночного ресурса на клетку (0…1). ~0.006 ≈ 1 на 170 клеток.")]
        [SerializeField] float resourceScatterChance = 0.006f;

        [Header("Черви")]
        [Tooltip("Сектор = resourceClusterSpacing. Черви у жил и wild в пустых секторах.")]

        [SerializeField] int wormLengthMin = 5;
        [SerializeField] int wormLengthMax = 15;

        [Tooltip("Черви только глубже этого % от R.")]
        [SerializeField] float wormMinDepthPercent = 0f;

        [Tooltip("Шанс одного wild-червя в секторе без жилы (0…1).")]
        [SerializeField] float wormWildChance = 0.25f;

        [Tooltip("Минимум червей в секторе (wild + у жилы). 1 = хотя бы один на сектор.")]
        [SerializeField] int wormMinPerSector = 1;

        [Tooltip("Дрон ближе этого радиуса (клетки) — червь просыпается.")]
        [SerializeField] int wormWakeRadius = 14;

        [Tooltip("Интервал тика движения, сек.")]
        [SerializeField] float wormTickInterval = 0.35f;

        [Tooltip("Шанс начать атаку у червя на жиле, когда виден и в aggro (0…1).")]
        [SerializeField] float wormVeinAggressiveChance = 0.75f;

        [Tooltip("Шанс начать атаку у wild-червя (0…1).")]
        [SerializeField] float wormWildAggressiveChance = 0.5f;

        [Tooltip("Длительность окна атаки, сек (мин).")]
        [SerializeField] float wormAttackDurationMin = 15f;

        [Tooltip("Длительность окна атаки, сек (макс).")]
        [SerializeField] float wormAttackDurationMax = 30f;

        [Tooltip("Пауза после неудачного кубика атаки, сек.")]
        [SerializeField] float wormAttackRetryCooldown = 15f;

        [Tooltip("Шанс начать серию рывков за шаг атаки (0…1).")]
        [SerializeField] float wormAttackBurstChance = 0.35f;

        [Tooltip("Радиус ambient one-shot от дрона (клетки). Червь может быть за кадром.")]
        [SerializeField] int wormAudioRadius = 50;

        [Tooltip("Радиус перехода агрессивного червя в Attack, клетки.")]
        [SerializeField] int wormAggroRadius = 10;

        [Tooltip("Радиус потери Attack. Должен быть больше aggro, чтобы не мигать на границе.")]
        [SerializeField] int wormLoseAggroRadius = 16;

        [Tooltip("Пауза после рывка до следующей подготовки атаки, сек.")]
        [SerializeField] float wormAttackCooldown = 3f;

        [Tooltip("Сколько секунд червь предупреждает перед рывком.")]
        [SerializeField] float wormChargeDuration = 0.8f;

        [Tooltip("Скорость движения во время подготовки: секунд на клетку.")]
        [SerializeField] float wormChargeStepInterval = 0.35f;

        [Tooltip("Скорость рывка: секунд на клетку.")]
        [SerializeField] float wormDashStepInterval = 0.12f;

        [Tooltip("Длина рывка в клетках.")]
        [SerializeField] int wormDashLength = 4;

        [Header("Магнитные аномалии")]
        [Tooltip("Сектор = 100 клеток (HUD). Спавн на грубой сетке блоков.")]
        [SerializeField] int magneticSectorCells = 100;

        [Tooltip("Размер блока спавна в секторах (3 = ролл каждые 300×300 клеток).")]
        [SerializeField] int magneticSpawnBlockSectors = 3;

        [Tooltip("Шанс аномалии в блоке (0…1).")]
        [SerializeField] float magneticSectorChance = 0.35f;

        [Tooltip("Аномалии только глубже этого % от R.")]
        [SerializeField] float magneticMinDepthPercent = 5f;

        [Tooltip("Шанс маленькой аномалии среди успешных спавнов (0…1).")]
        [SerializeField] float magneticSmallChance = 0.55f;

        [SerializeField] float magneticSmallRadiusMinSectors = 0.4f;
        [SerializeField] float magneticSmallRadiusMaxSectors = 1f;
        [SerializeField] int magneticSmallRoamMinCells = 12;
        [SerializeField] int magneticSmallRoamMaxCells = 35;
        [SerializeField] int magneticLargeRoamMinCells = 60;
        [SerializeField] int magneticLargeRoamMaxCells = 140;

        [SerializeField] int magneticRadiusXMinSectors = 2;
        [SerializeField] int magneticRadiusXMaxSectors = 4;
        [SerializeField] int magneticRadiusYMinSectors = 2;
        [SerializeField] int magneticRadiusYMaxSectors = 5;

        [Tooltip("Мин. расстояние между центрами, клетки.")]
        [SerializeField] int magneticMinSeparationCells = 80;

        [SerializeField] float magneticDriftSpeedMin = 0.8f;
        [SerializeField] float magneticDriftSpeedMax = 2.5f;
        [SerializeField] float magneticDirectionChangeMin = 8f;
        [SerializeField] float magneticDirectionChangeMax = 18f;

        [SerializeField] float magneticBoostMultiplier = 1.5f;
        [SerializeField] float magneticSlowMultiplier = 0.45f;
        [SerializeField] float magneticAgainstMultiplier = 0.08f;

        public int WorldSeed => worldSeed;
        public bool RandomWorldSeed => randomWorldSeed;
        public int WorldRadius => worldRadius;
        public float[] LayerDepthPercents => layerDepthPercents;
        public float LayerBoundaryWarpPercent => layerBoundaryWarpPercent;
        public float LayerWarpScaleX => layerWarpScaleX;
        public float LayerWarpScaleY => layerWarpScaleY;
        public int InclusionClusterSpacing => inclusionClusterSpacing;
        public int InclusionClusterSizeMin => inclusionClusterSizeMin;
        public int InclusionClusterSizeMax => inclusionClusterSizeMax;
        public int InclusionClusterRadiusMin => inclusionClusterRadiusMin;
        public int InclusionClusterRadiusMax => inclusionClusterRadiusMax;
        public float InclusionClusterChance => inclusionClusterChance;
        public int InclusionMaxTierDelta => inclusionMaxTierDelta;
        public int ResourceClusterSpacing => resourceClusterSpacing;
        public int ResourceClusterSizeMin => resourceClusterSizeMin;
        public int ResourceClusterSizeMax => resourceClusterSizeMax;
        public int ResourceClusterRadiusMin => resourceClusterRadiusMin;
        public int ResourceClusterRadiusMax => resourceClusterRadiusMax;
        public float ResourceClusterChance => resourceClusterChance;
        public float ResourceDeVaultMinDepthPercent => resourceDeVaultMinDepthPercent;
        public float ResourceDeVaultClusterWeight => resourceDeVaultClusterWeight;
        public float ResourceScatterChance => resourceScatterChance;
        public int WormLengthMin => wormLengthMin;
        public int WormLengthMax => wormLengthMax;
        public float WormMinDepthPercent => wormMinDepthPercent;
        public float WormWildChance => wormWildChance;
        public int WormMinPerSector => wormMinPerSector;
        public int WormWakeRadius => wormWakeRadius;
        public float WormTickInterval => wormTickInterval;
        public float WormVeinAggressiveChance => wormVeinAggressiveChance;
        public float WormWildAggressiveChance => wormWildAggressiveChance;
        public float WormAttackDurationMin => wormAttackDurationMin;
        public float WormAttackDurationMax => wormAttackDurationMax;
        public float WormAttackRetryCooldown => wormAttackRetryCooldown;
        public float WormAttackBurstChance => wormAttackBurstChance;
        public int WormAudioRadius => wormAudioRadius;
        public int WormAggroRadius => wormAggroRadius;
        public int WormLoseAggroRadius => wormLoseAggroRadius;
        public float WormAttackCooldown => wormAttackCooldown;
        public float WormChargeDuration => wormChargeDuration;
        public float WormChargeStepInterval => wormChargeStepInterval;
        public float WormDashStepInterval => wormDashStepInterval;
        public int WormDashLength => wormDashLength;
        public int MagneticSectorCells => magneticSectorCells;
        public int MagneticSpawnBlockSectors => magneticSpawnBlockSectors;
        public float MagneticSectorChance => magneticSectorChance;
        public float MagneticMinDepthPercent => magneticMinDepthPercent;
        public float MagneticSmallChance => magneticSmallChance;
        public float MagneticSmallRadiusMinSectors => magneticSmallRadiusMinSectors;
        public float MagneticSmallRadiusMaxSectors => magneticSmallRadiusMaxSectors;
        public int MagneticSmallRoamMinCells => magneticSmallRoamMinCells;
        public int MagneticSmallRoamMaxCells => magneticSmallRoamMaxCells;
        public int MagneticLargeRoamMinCells => magneticLargeRoamMinCells;
        public int MagneticLargeRoamMaxCells => magneticLargeRoamMaxCells;
        public int MagneticRadiusXMinSectors => magneticRadiusXMinSectors;
        public int MagneticRadiusXMaxSectors => magneticRadiusXMaxSectors;
        public int MagneticRadiusYMinSectors => magneticRadiusYMinSectors;
        public int MagneticRadiusYMaxSectors => magneticRadiusYMaxSectors;
        public int MagneticMinSeparationCells => magneticMinSeparationCells;
        public float MagneticDriftSpeedMin => magneticDriftSpeedMin;
        public float MagneticDriftSpeedMax => magneticDriftSpeedMax;
        public float MagneticDirectionChangeMin => magneticDirectionChangeMin;
        public float MagneticDirectionChangeMax => magneticDirectionChangeMax;
        public float MagneticBoostMultiplier => magneticBoostMultiplier;
        public float MagneticSlowMultiplier => magneticSlowMultiplier;
        public float MagneticAgainstMultiplier => magneticAgainstMultiplier;

        void OnValidate()
        {
            worldRadius = Mathf.Max(1, worldRadius);
            inclusionClusterSizeMax = Mathf.Max(inclusionClusterSizeMin, inclusionClusterSizeMax);
            inclusionClusterRadiusMax = Mathf.Max(inclusionClusterRadiusMin, inclusionClusterRadiusMax);
            inclusionClusterRadiusMin = Mathf.Max(1, inclusionClusterRadiusMin);
            inclusionClusterSpacing = Mathf.Max(1, inclusionClusterSpacing);
            inclusionClusterChance = Mathf.Clamp01(inclusionClusterChance);
            inclusionMaxTierDelta = Mathf.Max(1, inclusionMaxTierDelta);
            resourceClusterSizeMax = Mathf.Max(resourceClusterSizeMin, resourceClusterSizeMax);
            resourceClusterRadiusMax = Mathf.Max(resourceClusterRadiusMin, resourceClusterRadiusMax);
            resourceClusterRadiusMin = Mathf.Max(1, resourceClusterRadiusMin);
            resourceClusterSpacing = Mathf.Max(1, resourceClusterSpacing);
            resourceClusterChance = Mathf.Clamp01(resourceClusterChance);
            resourceDeVaultMinDepthPercent = Mathf.Clamp(resourceDeVaultMinDepthPercent, 0f, 100f);
            resourceDeVaultClusterWeight = Mathf.Clamp01(resourceDeVaultClusterWeight);
            resourceScatterChance = Mathf.Clamp01(resourceScatterChance);
            wormLengthMax = Mathf.Max(wormLengthMin, wormLengthMax);
            wormLengthMin = Mathf.Max(1, wormLengthMin);
            wormMinDepthPercent = Mathf.Clamp(wormMinDepthPercent, 0f, 100f);
            wormWildChance = Mathf.Clamp01(wormWildChance);
            wormMinPerSector = Mathf.Max(0, wormMinPerSector);
            wormWakeRadius = Mathf.Max(1, wormWakeRadius);
            wormTickInterval = Mathf.Max(0.05f, wormTickInterval);
            wormVeinAggressiveChance = Mathf.Clamp01(wormVeinAggressiveChance);
            wormWildAggressiveChance = Mathf.Clamp01(wormWildAggressiveChance);
            wormAttackDurationMax = Mathf.Max(wormAttackDurationMin, wormAttackDurationMax);
            wormAttackDurationMin = Mathf.Max(1f, wormAttackDurationMin);
            wormAttackRetryCooldown = Mathf.Max(0f, wormAttackRetryCooldown);
            wormAttackBurstChance = Mathf.Clamp01(wormAttackBurstChance);
            wormAudioRadius = Mathf.Max(wormWakeRadius, wormAudioRadius);
            wormAggroRadius = Mathf.Max(1, wormAggroRadius);
            wormLoseAggroRadius = Mathf.Max(wormAggroRadius, wormLoseAggroRadius);
            wormAttackCooldown = Mathf.Max(0f, wormAttackCooldown);
            wormChargeDuration = Mathf.Max(0.05f, wormChargeDuration);
            wormChargeStepInterval = Mathf.Max(0.05f, wormChargeStepInterval);
            wormDashStepInterval = Mathf.Max(0.03f, wormDashStepInterval);
            wormDashLength = Mathf.Max(1, wormDashLength);
            magneticSectorCells = Mathf.Max(1, magneticSectorCells);
            magneticSpawnBlockSectors = Mathf.Max(1, magneticSpawnBlockSectors);
            magneticSectorChance = Mathf.Clamp01(magneticSectorChance);
            magneticMinDepthPercent = Mathf.Clamp(magneticMinDepthPercent, 0f, 100f);
            magneticSmallChance = Mathf.Clamp01(magneticSmallChance);
            magneticSmallRadiusMaxSectors = Mathf.Max(magneticSmallRadiusMinSectors, magneticSmallRadiusMaxSectors);
            magneticSmallRadiusMinSectors = Mathf.Max(0.1f, magneticSmallRadiusMinSectors);
            magneticSmallRoamMaxCells = Mathf.Max(magneticSmallRoamMinCells, magneticSmallRoamMaxCells);
            magneticSmallRoamMinCells = Mathf.Max(1, magneticSmallRoamMinCells);
            magneticLargeRoamMaxCells = Mathf.Max(magneticLargeRoamMinCells, magneticLargeRoamMaxCells);
            magneticLargeRoamMinCells = Mathf.Max(1, magneticLargeRoamMinCells);
            magneticRadiusXMaxSectors = Mathf.Max(magneticRadiusXMinSectors, magneticRadiusXMaxSectors);
            magneticRadiusYMaxSectors = Mathf.Max(magneticRadiusYMinSectors, magneticRadiusYMaxSectors);
            magneticRadiusXMinSectors = Mathf.Max(1, magneticRadiusXMinSectors);
            magneticRadiusYMinSectors = Mathf.Max(1, magneticRadiusYMinSectors);
            magneticMinSeparationCells = Mathf.Max(0, magneticMinSeparationCells);
            magneticDriftSpeedMax = Mathf.Max(magneticDriftSpeedMin, magneticDriftSpeedMax);
            magneticDriftSpeedMin = Mathf.Max(0.01f, magneticDriftSpeedMin);
            magneticDirectionChangeMax = Mathf.Max(magneticDirectionChangeMin, magneticDirectionChangeMax);
            magneticDirectionChangeMin = Mathf.Max(1f, magneticDirectionChangeMin);
            magneticBoostMultiplier = Mathf.Max(0.01f, magneticBoostMultiplier);
            magneticSlowMultiplier = Mathf.Clamp(magneticSlowMultiplier, 0.01f, 1f);
            magneticAgainstMultiplier = Mathf.Clamp(magneticAgainstMultiplier, 0.01f, 1f);
        }

        /// <summary>Сдвиг порога границы слоя layerIndex в процентах глубины.</summary>
        public float GetLayerBoundaryShiftPercent(int x, int y, int layerIndex, int worldSeed)
        {
            var seed = worldSeed * 0.01713f;
            var layerPhase = layerIndex * 19.7f + seed * 53.1f;
            var nx = (x + layerPhase) * layerWarpScaleX;
            var ny = (y + layerPhase * 0.63f) * layerWarpScaleY;
            var noise = Mathf.PerlinNoise(nx, ny);
            return (noise - 0.5f) * 2f * layerBoundaryWarpPercent;
        }
    }
}
