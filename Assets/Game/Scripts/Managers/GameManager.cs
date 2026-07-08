using Operator.Bootstrap;
using Operator.Data;
using Operator.Depth.Core;
using UnityEngine;

namespace Operator.Managers
{
    /// <summary>
    /// Состояние забега на сцене уровня.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public World World { get; set; }

        public WormWorld WormWorld { get; set; }

        public MagneticAnomalyWorld MagneticAnomalyWorld { get; set; }

        public AssetManager Assets => GameBootstrap.Instance.Assets;
        public WorldSession Session => GameBootstrap.Instance.Session;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }
    }
}
