using UnityEngine;

namespace Operator.Audio.Unity
{
    /// <summary>
    /// Loop ambient на центре аномалии. Позиция — transform (overlay двигает родителя).
    /// Громкость по дистанции — через 3D rolloff AudioSource, как у червя.
    /// </summary>
    public class MagneticAnomalyAmbientAudio : MonoBehaviour
    {
        [SerializeField] AudioSource audioSource;

        void Awake()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }

            PrepareAudio();
        }

        void OnEnable()
        {
            if (audioSource == null || audioSource.clip == null)
            {
                return;
            }

            audioSource.time = Random.Range(0f, audioSource.clip.length);
            audioSource.Play();
        }

        void OnDisable()
        {
            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.Stop();
            }
        }

        void PrepareAudio()
        {
            if (audioSource == null)
            {
                return;
            }

            audioSource.playOnAwake = false;
            audioSource.loop = true;
            audioSource.spatialBlend = 1f;

            if (audioSource.isPlaying)
            {
                audioSource.Stop();
            }
        }
    }
}
