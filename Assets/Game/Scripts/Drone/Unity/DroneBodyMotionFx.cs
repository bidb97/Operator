using Operator.Depth.Core;
using Operator.Managers;
using UnityEngine;

namespace Operator.Drone.Unity
{
    /// <summary>
    /// Покачивание корпуса: idle-парение, отставание от тяги, микровибрация, отдача при бурении, scale idle/drive.
    /// Двигает только localPosition/localScale дочернего корпуса, не корень дрона.
    /// </summary>
    public class DroneBodyMotionFx : MonoBehaviour
    {
        [SerializeField] DroneController drone;
        [SerializeField] Transform bodyTransform;
        [SerializeField] float visualSmooth = 12f;
        [SerializeField] float garageFadeDuration = 0.35f;

        [Header("Idle")]
        [SerializeField] float idleBobAmplitudeX = 0.025f;
        [SerializeField] float idleBobAmplitudeY = 0.04f;
        [SerializeField] float idleBobPeriod = 2.2f;
        [SerializeField] float rotateIdleMultiplier = 1.15f;

        [Header("Drive")]
        [SerializeField] Vector2 thrustLagDirection = new(0f, -1f);
        [SerializeField] float driveThrustLagAmount = 0.4f;
        [SerializeField] float driveReleaseDelay = 0.12f;
        [SerializeField] float microVibeAmplitude = 0.012f;
        [SerializeField] float microVibeFrequency = 28f;

        [Header("Drill")]
        [SerializeField] float drillThrustLagAmount = 0f;
        [SerializeField] float drillRecoilAmount = 0.035f;
        [SerializeField] float drillRecoilWaves = 2.5f;

        [Header("Scale")]
        [SerializeField] float idleBodyScale = 0.96f;
        [SerializeField] float driveBodyScale = 1.02f;

        Vector3 _baseLocalPosition;
        Vector3 _baseLocalScale;
        Vector3 _smoothedThrustOffset;
        float _motionStrength;
        float _driveHold;
        float _idleTime;

        public Vector2 BodyVisualOffset =>
            bodyTransform != null
                ? (Vector2)(bodyTransform.localPosition - _baseLocalPosition)
                : Vector2.zero;

        void Awake()
        {
            if (drone == null)
            {
                drone = GetComponent<DroneController>();
            }

            if (bodyTransform == null)
            {
                bodyTransform = FindBodyTransform();
            }

            if (bodyTransform != null)
            {
                _baseLocalPosition = bodyTransform.localPosition;
                _baseLocalScale = bodyTransform.localScale;
            }
        }

        void Update()
        {
            if (bodyTransform == null || drone == null)
            {
                return;
            }

            var inGarage = IsInGarage();
            var working = drone.IsMoving || drone.IsDrilling || drone.IsRotating;
            var active = !inGarage || working;
            var fadeStep = garageFadeDuration > 0f ? Time.deltaTime / garageFadeDuration : 1f;
            _motionStrength = Mathf.MoveTowards(_motionStrength, active ? 1f : 0f, fadeStep);
            UpdateDriveHold();

            var t = 1f - Mathf.Exp(-visualSmooth * Time.deltaTime);

            if (_motionStrength <= 0.001f)
            {
                bodyTransform.localPosition = Vector3.Lerp(bodyTransform.localPosition, _baseLocalPosition, t);
                bodyTransform.localScale = Vector3.Lerp(bodyTransform.localScale, _baseLocalScale, t);
                return;
            }

            _idleTime += Time.deltaTime;
            var offset = Vector3.zero;

            if (ShouldIdleBob())
            {
                offset += IdleBobOffset();
            }

            if (_driveHold > 0.001f)
            {
                offset += ThrustLagOffset();
            }

            if (_driveHold > 0.001f && drone.IsMoving)
            {
                offset += MicroVibeOffset() * _driveHold;
            }

            if (drone.IsDrilling)
            {
                offset += DrillRecoilOffset();
            }

            offset *= _motionStrength;

            var targetPosition = _baseLocalPosition + offset;
            bodyTransform.localPosition = Vector3.Lerp(bodyTransform.localPosition, targetPosition, t);
            bodyTransform.localScale = Vector3.Lerp(
                bodyTransform.localScale,
                _baseLocalScale * ResolveBodyScaleMultiplier(),
                t);
        }

        void UpdateDriveHold()
        {
            if (drone.IsMoving || drone.IsDrilling)
            {
                _driveHold = 1f;
                return;
            }

            var decay = driveReleaseDelay > 0f ? Time.deltaTime / driveReleaseDelay : 1f;
            _driveHold = Mathf.MoveTowards(_driveHold, 0f, decay);
        }

        float ResolveBodyScaleMultiplier()
        {
            if (_motionStrength <= 0.001f)
            {
                return 1f;
            }

            return Mathf.Lerp(idleBodyScale, driveBodyScale, _driveHold);
        }

        bool ShouldIdleBob()
        {
            if (drone.IsRotating)
            {
                return true;
            }

            return _driveHold < 0.001f && !drone.IsDrilling;
        }

        Vector3 IdleBobOffset()
        {
            if (idleBobPeriod <= 0f)
            {
                return Vector3.zero;
            }

            var omega = Mathf.PI * 2f / idleBobPeriod;
            var ampScale = drone.IsRotating ? rotateIdleMultiplier : 1f;
            var x = Mathf.Sin(_idleTime * omega) * idleBobAmplitudeX * ampScale;
            var y = Mathf.Sin(_idleTime * omega * 0.87f + 0.9f) * idleBobAmplitudeY * ampScale;
            return new Vector3(x, y, 0f);
        }

        Vector3 ThrustLagOffset()
        {
            var direction = thrustLagDirection.sqrMagnitude > 0.0001f
                ? thrustLagDirection.normalized
                : Vector2.down;
            var amount = drone.IsDrilling ? drillThrustLagAmount : driveThrustLagAmount;
            var target = (Vector3)(direction * (amount * _driveHold));
            var t = 1f - Mathf.Exp(-visualSmooth * Time.deltaTime);
            _smoothedThrustOffset = Vector3.Lerp(_smoothedThrustOffset, target, t);
            return _smoothedThrustOffset;
        }

        Vector3 MicroVibeOffset()
        {
            if (microVibeAmplitude <= 0f || microVibeFrequency <= 0f)
            {
                return Vector3.zero;
            }

            var time = Time.time * microVibeFrequency;
            var x = Mathf.Sin(time * 1.07f) * microVibeAmplitude;
            var y = Mathf.Sin(time * 1.31f + 1.4f) * microVibeAmplitude;
            return new Vector3(x, y, 0f);
        }

        Vector3 DrillRecoilOffset()
        {
            if (drillRecoilAmount <= 0f)
            {
                return Vector3.zero;
            }

            var direction = thrustLagDirection.sqrMagnitude > 0.0001f
                ? thrustLagDirection.normalized
                : Vector2.down;
            var wave = Mathf.Sin(drone.DrillProgress * Mathf.PI * 2f * drillRecoilWaves);
            return (Vector3)(direction * (wave * drillRecoilAmount));
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

        Transform FindBodyTransform()
        {
            foreach (Transform child in transform)
            {
                if (child.name == "Sprite" || child.name == "Bur")
                {
                    return child;
                }
            }

            return null;
        }
    }
}
