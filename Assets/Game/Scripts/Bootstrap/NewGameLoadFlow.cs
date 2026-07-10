using UnityEngine;

namespace Operator.Bootstrap
{
    /// <summary>
    /// Сигнал Main Menu → LevelBootstrap / Intro: этапы New Game.
    /// </summary>
    public static class NewGameLoadFlow
    {
        public static bool BuildRequested { get; set; }
        public static bool BuildFinished { get; set; }
        public static bool IntroFinished { get; set; }
        public static float BuildProgress { get; set; }

        public static bool IsBuilding => BuildRequested && !BuildFinished;

        public static void ResetForNewGame()
        {
            BuildRequested = false;
            BuildFinished = false;
            IntroFinished = false;
            BuildProgress = 0f;
        }

        public static void SetWorldProgress(float worldProgress) =>
            BuildProgress = Mathf.Clamp01(worldProgress);
    }
}
