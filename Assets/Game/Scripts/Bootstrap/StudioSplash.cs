using UnityEngine;
using UnityEngine.SceneManagement;

namespace Operator.Bootstrap
{
    public class StudioSplash : MonoBehaviour
    {
        [SerializeField] float delaySeconds = 2.5f;
        [SerializeField] string nextSceneName = "Main Menu";

        float _elapsed;
        bool _loading;

        void Update()
        {
            if (_loading)
                return;

            _elapsed += Time.deltaTime;

            if (_elapsed >= delaySeconds)
                LoadNext();
        }

        void LoadNext()
        {
            _loading = true;
            SceneManager.LoadScene(nextSceneName);
        }
    }
}
