using UnityEngine;

namespace Operator.Drone.LaserBeam.Unity
{
    public class LaserBeamController : MonoBehaviour
    {
        [SerializeField] LineRenderer lineRenderer;
        [SerializeField] Transform particleRoot;
        [SerializeField] Material particleMaterial;

        Vector3 _startLocal;
        Vector3 _endLocal;
        Vector3 _lastOffset = new(float.NaN, float.NaN, float.NaN);
        Vector3 _hitOffset;
        ParticleSystem[] _particles;
        bool _emitterVelocityFixed;
        bool _isDrillingActive;

        void Awake()
        {
            EnsureReferences();
            FixEmitterVelocityOnce();

            if (lineRenderer != null)
            {
                lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lineRenderer.receiveShadows = false;
            }

            CacheLinePositions();
        }

        void OnEnable()
        {
            EnsureReferences();
            FixEmitterVelocityOnce();
            ApplyParticleMaterial();
            CacheLinePositions();
            UpdateParticlePose();
        }

        void EnsureReferences()
        {
            if (lineRenderer == null)
            {
                lineRenderer = GetComponent<LineRenderer>();
            }

            if (particleRoot == null)
            {
                particleRoot = transform.Find("Crumbs");
            }

            if (_particles == null || _particles.Length == 0)
            {
                _particles = GetComponentsInChildren<ParticleSystem>(true);
            }
        }

        public void SetDrillingActive(bool active)
        {
            _isDrillingActive = active;

            if (active)
            {
                if (!gameObject.activeSelf)
                {
                    gameObject.SetActive(true);
                }

                if (lineRenderer != null)
                {
                    lineRenderer.enabled = true;
                }

                SetParticleEmission(true);
                CacheLinePositions();
                _hitOffset = Vector3.zero;
                _lastOffset = new Vector3(float.NaN, float.NaN, float.NaN);
                UpdateParticlePose();
                return;
            }

            if (lineRenderer != null)
            {
                lineRenderer.enabled = false;
            }

            SetParticleEmission(false);
            ResetSweep();
        }

        public void ClearParticles()
        {
            _isDrillingActive = false;

            if (lineRenderer != null)
            {
                lineRenderer.enabled = false;
            }

            if (_particles == null)
            {
                return;
            }

            foreach (var particle in _particles)
            {
                if (particle == null)
                {
                    continue;
                }

                particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        public void ResetSweep()
        {
            if (lineRenderer == null)
            {
                return;
            }

            lineRenderer.SetPosition(0, _startLocal);
            lineRenderer.SetPosition(1, _endLocal);
            _hitOffset = Vector3.zero;
            _lastOffset = new Vector3(float.NaN, float.NaN, float.NaN);
            UpdateParticlePose();
        }

        public void SetHitOffset(Vector3 localOffset)
        {
            if (lineRenderer == null || !lineRenderer.enabled)
            {
                return;
            }

            if ((_lastOffset - localOffset).sqrMagnitude < 0.000025f)
            {
                return;
            }

            _lastOffset = localOffset;
            _hitOffset = localOffset;
            lineRenderer.SetPosition(0, _startLocal + localOffset * 0.3f);
            lineRenderer.SetPosition(1, _endLocal + localOffset);
            UpdateParticlePose();
        }

        void FixEmitterVelocityOnce()
        {
            if (_emitterVelocityFixed || _particles == null)
            {
                return;
            }

            foreach (var particle in _particles)
            {
                if (particle == null)
                {
                    continue;
                }

                var main = particle.main;
                main.emitterVelocityMode = ParticleSystemEmitterVelocityMode.Custom;
                main.emitterVelocity = Vector3.zero;
            }

            _emitterVelocityFixed = true;
        }

        void ApplyParticleMaterial()
        {
            if (particleMaterial == null || _particles == null)
            {
                return;
            }

            foreach (var particle in _particles)
            {
                if (particle == null)
                {
                    continue;
                }

                var renderer = particle.GetComponent<ParticleSystemRenderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = particleMaterial;
                }
            }
        }

        void UpdateParticlePose()
        {
            if (particleRoot == null || lineRenderer == null || lineRenderer.positionCount < 2)
            {
                return;
            }

            var burLocal = Flatten(lineRenderer.GetPosition(0));
            var tipLocal = Flatten(lineRenderer.GetPosition(1));
            particleRoot.localPosition = tipLocal;

            var toBur = burLocal - tipLocal;
            if (toBur.sqrMagnitude < 0.0001f)
            {
                return;
            }

            particleRoot.localRotation = Quaternion.FromToRotation(Vector3.up, toBur.normalized);
        }

        void SetParticleEmission(bool emitting)
        {
            if (_particles == null)
            {
                return;
            }

            foreach (var particle in _particles)
            {
                if (particle == null)
                {
                    continue;
                }

                if (emitting)
                {
                    particle.Play(true);
                    continue;
                }

                if (particle.isPlaying)
                {
                    particle.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                }
            }
        }

        void CacheLinePositions()
        {
            if (lineRenderer == null || lineRenderer.positionCount < 2)
            {
                return;
            }

            _startLocal = Flatten(lineRenderer.GetPosition(0));
            _endLocal = Flatten(lineRenderer.GetPosition(1));
        }

        static Vector3 Flatten(Vector3 point)
        {
            return new Vector3(point.x, point.y, 0f);
        }
    }
}
