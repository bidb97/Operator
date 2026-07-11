using Operator.Drone.Unity;
using UnityEngine;

namespace Operator.Bootstrap
{
    /// <summary>Точки автосохранения: приезд на базу, уход в фон, выход из игры.</summary>
    public class GameSaveTrigger : MonoBehaviour
    {
        [SerializeField] DroneController droneController;

        void OnEnable()
        {
            if (droneController != null)
                droneController.ArrivedAtBase += Save;
        }

        void OnDisable()
        {
            if (droneController != null)
                droneController.ArrivedAtBase -= Save;
        }

        void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
                Save();
        }

        void OnApplicationQuit()
        {
            Save();
        }

        void Save()
        {
            if (GameBootstrap.Instance == null || droneController == null)
                return;

            GameBootstrap.Instance.SaveSession(droneController.transform.position);
        }
    }
}
