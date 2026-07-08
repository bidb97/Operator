using Operator.Data;
using Operator.Managers;
using UnityEngine;

namespace Operator.Bootstrap
{
    /// <summary>
    /// Старт приложения: сервисы и сессия. DontDestroyOnLoad, сцена Bootstrap.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        public static GameBootstrap Instance { get; private set; }

        public AssetManager Assets { get; private set; }
        public TextManager Text { get; private set; }
        public AudioManager Audio { get; private set; }
        public WorldSession Session { get; private set; }
        public WorldGenConfig WorldGen => worldGenConfig;
        public DroneLimitsConfig DroneLimits => droneLimitsConfig;

        [SerializeField] WorldGenConfig worldGenConfig;
        [SerializeField] DroneLimitsConfig droneLimitsConfig;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            
            DontDestroyOnLoad(transform.root.gameObject);

            InitServices();
            Session = TryLoadSession() ?? CreateNewSession();
        }

        void InitServices()
        {
            Assets = new AssetManager();
            Text = new TextManager(Assets);
            Audio = new AudioManager(Assets, gameObject);
        }

        WorldSession TryLoadSession()
        {
            // TODO: загрузка save → WorldSession
            return null;
        }

        WorldSession CreateNewSession()
        {
            var config = worldGenConfig;
            var seed = config != null && !config.RandomWorldSeed
                ? config.WorldSeed
                : Random.Range(int.MinValue, int.MaxValue);

            var radius = config != null
                ? config.WorldRadius
                : 10000;

            return new WorldSession
            {
                Seed = seed,
                Radius = radius,
                RunNumber = 1
            };
        }
    }
}
