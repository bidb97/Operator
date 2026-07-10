using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace Operator.Bootstrap
{
    public static class GameScenePresentation
    {
        public static void SetEnabled(Scene scene, bool enabled)
        {
            if (!scene.IsValid())
                return;

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
                    canvas.enabled = enabled;

                foreach (var camera in root.GetComponentsInChildren<Camera>(true))
                    camera.enabled = enabled;

                foreach (var listener in root.GetComponentsInChildren<AudioListener>(true))
                    listener.enabled = enabled;

                foreach (var eventSystem in root.GetComponentsInChildren<EventSystem>(true))
                    eventSystem.enabled = enabled;

                foreach (var audio in root.GetComponentsInChildren<AudioSource>(true))
                {
                    if (!enabled)
                        audio.Stop();

                    audio.enabled = enabled;

                    if (enabled && audio.playOnAwake && !audio.isPlaying)
                        audio.Play();
                }
            }
        }

        public static void SetEnabled(string sceneName, bool enabled) =>
            SetEnabled(SceneManager.GetSceneByName(sceneName), enabled);
    }
}
