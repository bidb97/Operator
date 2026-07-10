using Operator.Bootstrap;
using Operator.Depth.Core;
using Operator.Drone.Unity;
using Operator.Managers;
using UnityEngine;

namespace Operator.Audio.Unity
{
    /// <summary>
    /// Мотор дрона: в гараже на стоянке — тишина; под землёй на стоянке — тихий idle;
    /// при движении/бурении — громче. Play On Awake — выкл.
    /// </summary>
    public class DroneEngineAudio : MonoBehaviour
    {
        [SerializeField] DroneController drone;
        [SerializeField] AudioSource engine;
        [SerializeField] float maxVolume = 1f;
        [SerializeField] float idleVolume = 0.35f;
        [SerializeField] float fadeDuration = 0.35f;
        [SerializeField] float pitchIdle = 0.8f;
        [SerializeField] float pitchDrive = 1f;
        [SerializeField] float pitchRotate = 0.96f;
        [SerializeField] float pitchDrill = 0.92f;
        [SerializeField] float pitchSmooth = 8f;

        void Awake()
        {
            if (drone == null)
            {
                drone = GetComponent<DroneController>();
            }

            if (engine == null)
            {
                engine = GetComponent<AudioSource>();
            }

            PrepareEngine();
        }

        void Update()
        {
            if (NewGameLoadFlow.IsBuilding)
                return;

            if (engine == null || drone == null || !engine.enabled)
                return;

            var working = drone.IsMoving || drone.IsDrilling || drone.IsRotating;
            var inGarage = IsInGarage();
            var targetVolume = ResolveVolume(working, inGarage);
            var step = fadeDuration > 0f ? maxVolume * Time.deltaTime / fadeDuration : maxVolume;
            engine.volume = Mathf.MoveTowards(engine.volume, targetVolume, step);

            if (engine.volume <= 0.001f)
            {
                if (engine.isPlaying)
                {
                    engine.Stop();
                }

                return;
            }

            if (!engine.isPlaying)
            {
                engine.Play();
            }

            var targetPitch = pitchIdle;
            if (drone.IsDrilling)
            {
                targetPitch = pitchDrill;
            }
            else if (drone.IsRotating)
            {
                targetPitch = pitchRotate;
            }
            else if (drone.IsMoving)
            {
                targetPitch = pitchDrive;
            }

            engine.pitch = Mathf.Lerp(engine.pitch, targetPitch, pitchSmooth * Time.deltaTime);
        }

        float ResolveVolume(bool working, bool inGarage)
        {
            if (inGarage && !working)
            {
                return 0f;
            }

            if (working)
            {
                return maxVolume;
            }

            return idleVolume;
        }

        bool IsInGarage()
        {
            var cell = WorldGrid.WorldToCell(drone.transform.position);
            var world = GameManager.Instance?.World;
            if (world != null)
            {
                cell.x = world.WrapX(cell.x);
            }

            return GarageBounds.Contains(cell.x, cell.y);
        }

        void PrepareEngine()
        {
            if (engine == null)
            {
                return;
            }

            engine.playOnAwake = false;
            engine.loop = true;
            engine.spatialBlend = 0f;
            engine.volume = 0f;
            engine.pitch = pitchIdle;
            engine.Stop();
        }
    }
}
