using UnityEngine;

namespace Operator.Data
{
    /// <summary>
    /// Лимиты дрона и коэффициенты расхода. GDD → «Модули», «Планирование миссий».
    /// Планировщик миссий и геймплей читают одни и те же значения.
    /// </summary>
    [CreateAssetMenu(fileName = "DroneLimits", menuName = "Operator/Drone Limits Config")]
    public class DroneLimitsConfig : ScriptableObject
    {
        [Header("Ёмкости (старт / до прокачки)")]
        [Tooltip("Топливо: запас хода за один спуск. Тратится при движении (в тоннеле — полностью, при бурении — × Drill Fuel Multiplier). 0 вне базы = смерть.")]
        [SerializeField] float fuelMax = 200f;

        [Tooltip("Заряд: энергия лазера за один спуск. Тратится только на бурение породы (тиер в Charge Per Rock Tier). 0 = в стену не пройти, по тоннелю можно.")]
        [SerializeField] float chargeMax = 120f;

        [Tooltip("Отсек: объём груза за один спуск. +1 за клетку жилы с рудой. Полный — руда не забирается, звук отбоя (как у непробиваемой стены).")]
        [SerializeField] float cargoMax = 20f;

        [Header("Расход — движение")]
        [Tooltip("Топливо за одну клетку воздуха (обычная езда по тоннелю).")]
        [SerializeField] float fuelPerCell = 1f;

        [Tooltip("Доля Fuel Per Cell при движении в режиме бурения (медленнее → меньше). 0.4 = 40% от обычной езды.")]
        [SerializeField] float drillFuelMultiplier = 0.4f;

        [Header("Расход — бурение")]
        [Tooltip("Заряд за вырезание одной клетки породы. 8 элементов: Edge, Sediment, Scale, Arc, Frame, Seam, Mantle, Halo (лазер 1…8).")]
        [SerializeField] float[] chargePerRockTier = { 1f, 2f, 3f, 4f, 6f, 8f, 10f, 14f };

        [Header("Груз")]
        [Tooltip("Сколько места в отсеке занимает одна добытная клетка жилы с рудой (обычно 1).")]
        [SerializeField] float cargoPerVeinCell = 1f;

        [Header("Миссии")]
        [Tooltip("Запас после «идеального» маршрута: VIYA не выдаёт миссию, если останется меньше этого %. 0.15 = 15% топлива, заряда и отсека.")]
        [SerializeField] float missionSafetyMargin = 0.15f;

        public float FuelMax => fuelMax;
        public float ChargeMax => chargeMax;
        public float CargoMax => cargoMax;
        public float FuelPerCell => fuelPerCell;
        public float DrillFuelMultiplier => drillFuelMultiplier;
        public float CargoPerVeinCell => cargoPerVeinCell;
        public float MissionSafetyMargin => missionSafetyMargin;

        public float GetChargeForRockTier(int tier)
        {
            if (chargePerRockTier == null || chargePerRockTier.Length == 0)
            {
                return 1f;
            }

            var index = Mathf.Clamp(tier - 1, 0, chargePerRockTier.Length - 1);
            return chargePerRockTier[index];
        }

        void OnValidate()
        {
            fuelMax = Mathf.Max(1f, fuelMax);
            chargeMax = Mathf.Max(1f, chargeMax);
            cargoMax = Mathf.Max(1f, cargoMax);
            fuelPerCell = Mathf.Max(0.01f, fuelPerCell);
            drillFuelMultiplier = Mathf.Clamp(drillFuelMultiplier, 0.01f, 1f);
            cargoPerVeinCell = Mathf.Max(0.01f, cargoPerVeinCell);
            missionSafetyMargin = Mathf.Clamp(missionSafetyMargin, 0f, 0.5f);

            if (chargePerRockTier == null || chargePerRockTier.Length != 8)
            {
                chargePerRockTier = new[] { 1f, 2f, 3f, 4f, 6f, 8f, 10f, 14f };
            }

            for (var i = 0; i < chargePerRockTier.Length; i++)
            {
                chargePerRockTier[i] = Mathf.Max(0.01f, chargePerRockTier[i]);
            }
        }
    }
}
