using System.IO;
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

        /// <summary>Позиция дрона из сейва. Null — новая игра, ставить в гараже как обычно.</summary>
        public Vector3? SavedDronePosition { get; private set; }

        /// <summary>Сейв найден и успешно прочитан — Main Menu показывает "Продолжить".</summary>
        public bool HasSave { get; private set; }

        [SerializeField] WorldGenConfig worldGenConfig;
        [SerializeField] DroneLimitsConfig droneLimitsConfig;

        static string SavePath => Path.Combine(Application.persistentDataPath, "save.json");

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
            if (!File.Exists(SavePath))
            {
                return null;
            }

            SaveData data;
            try
            {
                data = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Не удалось прочитать сейв: {e}");
                return null;
            }

            if (data == null)
            {
                return null;
            }

            SavedDronePosition = new Vector3(data.droneX, data.droneY, 0f);
            HasSave = true;

            return new WorldSession
            {
                Seed = data.seed,
                Radius = data.radius,
                RunNumber = data.runNumber,
                LaserTier = data.laserTier,
                NextMissionIndex = data.nextMissionIndex
            };
        }

        /// <summary>Сохраняет текущую сессию + позицию дрона. Вызывается снаружи (приезд на базу, пауза, выход).</summary>
        public void SaveSession(Vector3 dronePosition)
        {
            if (Session == null)
            {
                return;
            }

            var data = new SaveData
            {
                seed = Session.Seed,
                radius = Session.Radius,
                runNumber = Session.RunNumber,
                laserTier = Session.LaserTier,
                nextMissionIndex = Session.NextMissionIndex,
                droneX = dronePosition.x,
                droneY = dronePosition.y
            };

            File.WriteAllText(SavePath, JsonUtility.ToJson(data));
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
