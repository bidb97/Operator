using System;

namespace Operator.Data
{
    /// <summary>
    /// Плоская DTO-структура для сериализации сейва (JsonUtility).
    /// Не путать с <see cref="WorldSession"/> — это runtime-модель, здесь только то, что реально пишем на диск.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public int seed;
        public int radius;
        public int runNumber;
        public int laserTier;
        public int nextMissionIndex;

        public float droneX;
        public float droneY;
    }
}
