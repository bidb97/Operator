using Operator.Depth.Core;
using Operator.Drone.Unity;
using Operator.Managers;
using UnityEngine;

namespace Operator.Audio.Unity
{
    /// <summary>
    /// Кроссфейд Surface (гараж) ↔ Depth (шахта) по позиции дрона.
    /// Вешать на SceneAudio; клипы — на Audio Source Surface и Depth.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public class MusicZoneCrossfade : MonoBehaviour
    {
        [SerializeField] AudioSource surfaceMusic;
        [SerializeField] AudioSource depthMusic;
        [SerializeField] Transform listener;
        [SerializeField] float fadeDuration = 1f;
        [SerializeField] float surfaceMaxVolume = 1f;
        [SerializeField] float depthMaxVolume = 1f;

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

            Prepare(surfaceMusic);
            Prepare(depthMusic);
        }

        void Start()
        {
            ApplyTargets(instant: true);
            EnsurePlaying(surfaceMusic);
            EnsurePlaying(depthMusic);
        }

        void Update()
        {
            if (surfaceMusic == null || depthMusic == null || listener == null)
            {
                return;
            }

            EnsurePlaying(surfaceMusic);
            EnsurePlaying(depthMusic);

            var inGarage = IsInGarage(GetListenerCell());
            var targetSurface = inGarage ? surfaceMaxVolume : 0f;
            var targetDepth = inGarage ? 0f : depthMaxVolume;
            var surfaceStep = fadeDuration > 0f ? surfaceMaxVolume * Time.deltaTime / fadeDuration : surfaceMaxVolume;
            var depthStep = fadeDuration > 0f ? depthMaxVolume * Time.deltaTime / fadeDuration : depthMaxVolume;

            surfaceMusic.volume = Mathf.MoveTowards(surfaceMusic.volume, targetSurface, surfaceStep);
            depthMusic.volume = Mathf.MoveTowards(depthMusic.volume, targetDepth, depthStep);
        }

        void ApplyTargets(bool instant)
        {
            if (surfaceMusic == null || depthMusic == null || listener == null)
            {
                return;
            }

            var inGarage = IsInGarage(GetListenerCell());
            var targetSurface = inGarage ? surfaceMaxVolume : 0f;
            var targetDepth = inGarage ? 0f : depthMaxVolume;

            if (instant)
            {
                surfaceMusic.volume = targetSurface;
                depthMusic.volume = targetDepth;
            }
        }

        static void Prepare(AudioSource source)
        {
            if (source == null)
            {
                return;
            }

            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.Stop();
        }

        static void EnsurePlaying(AudioSource source)
        {
            if (source != null && !source.isPlaying)
            {
                source.Play();
            }
        }

        Vector2Int GetListenerCell()
        {
            var cell = WorldGrid.WorldToCell(listener.position);
            var world = GameManager.Instance?.World;
            if (world != null)
            {
                cell.x = world.WrapX(cell.x);
            }

            return cell;
        }

        static bool IsInGarage(Vector2Int cell) => GarageBounds.Contains(cell.x, cell.y);
    }
}
