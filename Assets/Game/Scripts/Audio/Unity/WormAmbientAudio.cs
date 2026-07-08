using UnityEngine;

namespace Operator.Audio.Unity
{
    /// <summary>
    /// Случайные one-shot на черве. Громкость по дистанции — через 3D rolloff Audio Source (Spatial Blend = 1).
    /// Позиция source каждый кадр = голова червя (LineRenderer position 0, world space).
    /// </summary>
    public class WormAmbientAudio : MonoBehaviour
    {
        [SerializeField] LineRenderer lineRenderer;
        [SerializeField] AudioSource audioSource;
        [SerializeField] AudioClip[] clips;
        [SerializeField] float minInterval = 15f;
        [SerializeField] float maxInterval = 35f;
        [SerializeField] float volume = 1f;

        float _timer;

        void Awake()
        {
            if (lineRenderer == null)
            {
                lineRenderer = GetComponent<LineRenderer>();
            }

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }

            PrepareAudio();
        }

        void OnEnable()
        {
            ScheduleNext();
        }

        void OnDisable()
        {
            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.Stop();
            }
        }

        void Update()
        {
            if (!IsActive())
            {
                return;
            }

            SyncHeadPosition();
            TickTimer();
        }

        bool IsActive()
        {
            return lineRenderer != null
                && lineRenderer.enabled
                && lineRenderer.positionCount > 0
                && audioSource != null
                && clips != null
                && clips.Length > 0;
        }

        void SyncHeadPosition()
        {
            transform.position = lineRenderer.useWorldSpace
                ? lineRenderer.GetPosition(0)
                : lineRenderer.transform.TransformPoint(lineRenderer.GetPosition(0));
        }

        void TickTimer()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f)
            {
                return;
            }

            PlayRandomClip();
            ScheduleNext();
        }

        void PlayRandomClip()
        {
            var clip = clips[Random.Range(0, clips.Length)];
            if (clip == null)
            {
                return;
            }

            audioSource.PlayOneShot(clip, volume);
        }

        void ScheduleNext()
        {
            var min = Mathf.Max(0.1f, minInterval);
            var max = Mathf.Max(min, maxInterval);
            _timer = Random.Range(min, max);
        }

        void PrepareAudio()
        {
            if (audioSource == null)
            {
                return;
            }

            audioSource.playOnAwake = false;
            audioSource.loop = false;

            if (audioSource.isPlaying)
            {
                audioSource.Stop();
            }
        }
    }
}
