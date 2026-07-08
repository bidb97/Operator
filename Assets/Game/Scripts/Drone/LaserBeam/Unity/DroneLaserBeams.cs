using Operator.Drone.Unity;
using UnityEngine;

namespace Operator.Drone.LaserBeam.Unity
{
    public class DroneLaserBeams : MonoBehaviour
    {
        [SerializeField] LaserBeamController[] beams;

        [Header("Body follow")]
        [SerializeField] DroneBodyMotionFx bodyMotionFx;
        [Tooltip("Насколько лучи повторяют сдвиг корпуса по X (влево-вправо).")]
        [SerializeField] float bodyFollowX = 1f;
        [Tooltip("Насколько лучи повторяют сдвиг корпуса по Y (вперёд-назад от тяги).")]
        [SerializeField] float bodyFollowY = 1f;

        [SerializeField] AudioSource laserAudio;
        [SerializeField] float laserVolume = 1f;
        [SerializeField] AudioSource rockAudio;
        [SerializeField] AudioClip[] rockClips;
        [SerializeField] float rockVolume = 1f;

        bool _rockDrillActive;
        int _lastRockClipIndex = -1;

        void Awake()
        {
            if (bodyMotionFx == null)
            {
                bodyMotionFx = GetComponent<DroneBodyMotionFx>();
            }

            EnsureBeams();
            PrepareLaserAudio();
            PrepareRockAudio();
        }

        void LateUpdate()
        {
            if (bodyMotionFx == null || beams == null)
            {
                return;
            }

            var delta = bodyMotionFx.BodyVisualOffset;
            var offset = new Vector3(delta.x * bodyFollowX, delta.y * bodyFollowY, 0f);
            foreach (var beam in beams)
            {
                if (beam == null)
                {
                    continue;
                }

                beam.transform.localPosition = offset;
            }
        }

        public void BeginDrill(float facingAngle, float duration, bool loop)
        {
            EnsureBeams();
            _rockDrillActive = true;
            SetBeamsDrillingActive(true);
            StartLaserSound();
            StartRockSound();
        }

        public void UpdateDrill(float deltaTime)
        {
            if (!_rockDrillActive || rockAudio == null || rockClips == null || rockClips.Length == 0)
            {
                return;
            }

            if (!rockAudio.isPlaying)
            {
                PlayNextRockClip();
            }
        }

        public void EndDrill()
        {
            SetBeamsDrillingActive(false);
        }

        public void StopDrillSound()
        {
            _rockDrillActive = false;
            _lastRockClipIndex = -1;
            EndDrill();
            StopLaserSound();
            StopRockSound();
        }

        public void ClearDrillParticles()
        {
            EnsureBeams();
            foreach (var beam in beams)
            {
                beam?.ClearParticles();
            }
        }

        void SetBeamsDrillingActive(bool active)
        {
            foreach (var beam in beams)
            {
                beam?.SetDrillingActive(active);
            }
        }

        void EnsureBeams()
        {
            if (beams != null && beams.Length > 0)
            {
                return;
            }

            beams = GetComponentsInChildren<LaserBeamController>(true);
        }

        void PrepareLaserAudio()
        {
            if (laserAudio == null)
            {
                return;
            }

            laserAudio.playOnAwake = false;
            laserAudio.spatialBlend = 0f;
        }

        void PrepareRockAudio()
        {
            if (rockAudio == null)
            {
                return;
            }

            rockAudio.playOnAwake = false;
            rockAudio.loop = false;
            rockAudio.spatialBlend = 0f;
        }

        void StartLaserSound()
        {
            if (laserAudio == null || laserAudio.clip == null)
            {
                return;
            }

            if (laserAudio.isPlaying)
            {
                return;
            }

            laserAudio.volume = laserVolume;
            laserAudio.Play();
        }

        void StartRockSound()
        {
            if (rockAudio == null || rockClips == null || rockClips.Length == 0)
            {
                return;
            }

            if (rockAudio.isPlaying)
            {
                return;
            }

            PlayNextRockClip();
        }

        void PlayNextRockClip()
        {
            if (rockAudio == null || rockClips == null || rockClips.Length == 0)
            {
                return;
            }

            var clip = PickRandomRockClip();
            if (clip == null)
            {
                return;
            }

            rockAudio.clip = clip;
            rockAudio.volume = rockVolume;
            rockAudio.Play();
        }

        AudioClip PickRandomRockClip()
        {
            if (rockClips.Length == 1)
            {
                _lastRockClipIndex = 0;
                return rockClips[0];
            }

            var index = Random.Range(0, rockClips.Length);
            if (index == _lastRockClipIndex)
            {
                index = (index + 1) % rockClips.Length;
            }

            _lastRockClipIndex = index;
            return rockClips[index];
        }

        void StopLaserSound()
        {
            if (laserAudio != null && laserAudio.isPlaying)
            {
                laserAudio.Stop();
            }
        }

        void StopRockSound()
        {
            if (rockAudio != null && rockAudio.isPlaying)
            {
                rockAudio.Stop();
            }
        }
    }
}
