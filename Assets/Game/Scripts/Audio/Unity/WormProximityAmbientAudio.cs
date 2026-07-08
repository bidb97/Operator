using System.Collections.Generic;
using Operator.Bootstrap;
using Operator.Depth.Core;
using Operator.Drone.Unity;
using Operator.Managers;
using UnityEngine;

namespace Operator.Audio.Unity
{
    /// <summary>
    /// Ambient one-shot червей от дрона: слышно в радиусе wormAudioRadius, даже если червь за кадром.
    /// Не привязано к WormsOverlay / prefab.
    /// </summary>
    [DefaultExecutionOrder(202)]
    public class WormProximityAmbientAudio : MonoBehaviour
    {
        [SerializeField] Transform listener;
        [SerializeField] AudioClip[] clips;
        [SerializeField] float minInterval = 4f;
        [SerializeField] float maxInterval = 12f;
        [SerializeField] float volume = 1f;

        readonly Dictionary<long, float> _timers = new();
        AudioSource _source;

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

            _source = GetComponent<AudioSource>();
            if (_source == null)
            {
                _source = gameObject.AddComponent<AudioSource>();
            }

            _source.playOnAwake = false;
            _source.loop = false;
            _source.spatialBlend = 0f;
        }

        void Update()
        {
            if (listener == null || clips == null || clips.Length == 0)
            {
                return;
            }

            var gameManager = GameManager.Instance;
            var world = gameManager?.World;
            var wormWorld = gameManager?.WormWorld;
            var config = GameBootstrap.Instance?.WorldGen;
            if (world == null || wormWorld == null || config == null)
            {
                return;
            }

            if (IsInGarage(world))
            {
                return;
            }

            var droneCell = WorldGrid.WorldToCell(listener.position);
            droneCell.x = world.WrapX(droneCell.x);
            var audioRadius = config.WormAudioRadius;
            wormWorld.EnsureNearCell(droneCell, audioRadius);

            var audioRadiusSq = audioRadius * audioRadius;
            var heard = new HashSet<long>();

            foreach (var worm in wormWorld.Worms)
            {
                if (worm.Segments.Count == 0)
                {
                    continue;
                }

                var distSq = WormWorld.MinDistanceSqToCell(worm, droneCell, world.WorldRadius);
                if (distSq > audioRadiusSq)
                {
                    continue;
                }

                heard.Add(worm.Id);
                TickWormAmbient(worm, distSq, audioRadiusSq);
            }

            PruneTimers(heard);
        }

        void TickWormAmbient(Worm worm, int distSq, int audioRadiusSq)
        {
            if (!_timers.TryGetValue(worm.Id, out var timer))
            {
                timer = Random.Range(minInterval, maxInterval) * 0.5f;
            }

            timer -= Time.deltaTime;
            if (timer > 0f)
            {
                _timers[worm.Id] = timer;
                return;
            }

            PlayForWorm(worm, distSq, audioRadiusSq);
            _timers[worm.Id] = Random.Range(
                Mathf.Max(0.5f, minInterval),
                Mathf.Max(minInterval, maxInterval));
        }

        void PlayForWorm(Worm worm, int distSq, int audioRadiusSq)
        {
            var clip = clips[Random.Range(0, clips.Length)];
            if (clip == null)
            {
                return;
            }

            var proximity = 1f - Mathf.Clamp01((float)distSq / audioRadiusSq);
            proximity *= proximity;
            var scaledVolume = volume * proximity;
            if (scaledVolume <= 0.01f)
            {
                return;
            }

            _source.PlayOneShot(clip, scaledVolume);
        }

        void PruneTimers(HashSet<long> heard)
        {
            if (_timers.Count == 0)
            {
                return;
            }

            var toRemove = new List<long>();
            foreach (var pair in _timers)
            {
                if (!heard.Contains(pair.Key))
                {
                    toRemove.Add(pair.Key);
                }
            }

            for (var i = 0; i < toRemove.Count; i++)
            {
                _timers.Remove(toRemove[i]);
            }
        }

        bool IsInGarage(World world)
        {
            var cell = WorldGrid.WorldToCell(listener.position);
            cell.x = world.WrapX(cell.x);
            return GarageBounds.Contains(cell.x, cell.y);
        }
    }
}
