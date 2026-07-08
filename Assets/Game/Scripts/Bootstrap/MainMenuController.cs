using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Operator.Bootstrap
{
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField] Button newGameButton;
        [SerializeField] string introSceneName = "Intro";

        bool _loading;

        void Awake()
        {
            if (newGameButton != null)
                newGameButton.onClick.AddListener(StartNewGame);
        }

        public void StartNewGame()
        {
            if (_loading)
                return;

            _loading = true;
            SceneManager.LoadScene(introSceneName);
        }
    }
}
