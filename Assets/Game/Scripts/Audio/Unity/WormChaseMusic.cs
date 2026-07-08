using Operator.Bootstrap;
using Operator.Depth.Core;
using Operator.Drone.Unity;
using Operator.Managers;
using UnityEngine;

namespace Operator.Audio.Unity
{
    /// <summary>
    /// Тревожный loop, пока хотя бы один червь в Attack. Fade как у MusicZoneCrossfade.
    /// Клип — на Audio Source Chase Music (или создаётся на этом объекте).
    /// </summary>
    [DefaultExecutionOrder(201)]
    public class WormChaseMusic : MonoBehaviour
    {
        [SerializeField] AudioSource chaseMusic;
        [SerializeField] AudioClip chaseClip;
        [SerializeField] Transform listener;
        [SerializeField] float maxVolume = 0.55f;
        [SerializeField] float fadeDuration = 1.25f;

        void Awake()
        {
            if (listener == null)
            {
                var drone = FindFirstObjectByType<DroneController>();
                if (drone != null)
                {
                    listener = drone.transform;
                }
            }

            if (chaseMusic == null)
            {
                var child = new GameObject("WormChaseMusic");
                child.transform.SetParent(transform, false);
                chaseMusic = child.AddComponent<AudioSource>();
            }

            PrepareChaseMusic();

            if (chaseClip != null)
            {
                chaseMusic.clip = chaseClip;
            }
        }

        void Update()
        {
            if (chaseMusic == null || chaseMusic.clip == null || listener == null)
            {
                return;
            }

            var chasing = !IsInGarage() && IsAnyWormChasing();
            var targetVolume = chasing ? maxVolume : 0f;
            var step = fadeDuration > 0f ? maxVolume * Time.deltaTime / fadeDuration : maxVolume;
            chaseMusic.volume = Mathf.MoveTowards(chaseMusic.volume, targetVolume, step);

            if (chaseMusic.volume <= 0.001f)
            {
                if (chaseMusic.isPlaying)
                {
                    chaseMusic.Stop();
                }

                return;
            }

            if (!chaseMusic.isPlaying)
            {
                chaseMusic.Play();
            }
        }

        static bool IsAnyWormChasing() => GameManager.Instance?.WormWorld?.AnyWormChasing ?? false;

        bool IsInGarage()
        {
            var cell = WorldGrid.WorldToCell(listener.position);
            var world = GameManager.Instance?.World;
            if (world != null)
            {
                cell.x = world.WrapX(cell.x);
            }

            return GarageBounds.Contains(cell.x, cell.y);
        }

        void PrepareChaseMusic()
        {
            chaseMusic.playOnAwake = false;
            chaseMusic.loop = true;
            chaseMusic.spatialBlend = 0f;
            chaseMusic.volume = 0f;
            chaseMusic.Stop();
        }
    }
}
