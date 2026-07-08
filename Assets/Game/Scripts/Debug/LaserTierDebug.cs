using Operator.Depth.Core;
using Operator.Managers;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Operator.Dev
{
    /// <summary>DEV-хелпер: клавиши 1–8 задают тир лазера в текущей сессии. Удалить компонент перед релизом.</summary>
    public class LaserTierDebug : MonoBehaviour
    {
        void Update()
        {
            var keyboard = Keyboard.current;
            var session = GameManager.Instance?.Session;
            if (keyboard == null || session == null)
            {
                return;
            }

            for (var tier = 1; tier <= RockLayers.MaxLaserTier; tier++)
            {
                if (WasDigitPressed(keyboard, tier))
                {
                    session.LaserTier = tier;
                    return;
                }
            }
        }

        static bool WasDigitPressed(Keyboard keyboard, int digit)
        {
            var key = digit switch
            {
                1 => Key.Digit1,
                2 => Key.Digit2,
                3 => Key.Digit3,
                4 => Key.Digit4,
                5 => Key.Digit5,
                6 => Key.Digit6,
                7 => Key.Digit7,
                8 => Key.Digit8,
                _ => Key.None
            };

            return key != Key.None && keyboard[key].wasPressedThisFrame;
        }
    }
}
