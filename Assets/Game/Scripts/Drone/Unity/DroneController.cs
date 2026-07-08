using DG.Tweening;
using Operator.Depth.Core;
using Operator.Depth.Unity;
using Operator.Drone.LaserBeam.Unity;
using Operator.Managers;
using UnityEngine;

namespace Operator.Drone.Unity
{
    public class DroneController : MonoBehaviour
    {
        [SerializeField] float moveSpeed = 3f;
        [SerializeField] float garageMoveSpeed = 1f;
        [SerializeField] float drillDuration = DrillRules.DefaultDrillDuration;
        [SerializeField] float fullRotationDuration = 1f;
        [SerializeField] DroneLaserBeams laserBeams;
        [SerializeField] DepthWorld depthWorld;
        [SerializeField] AudioSource blockedDrillAudio;
        [SerializeField] AudioClip blockedDrillClip;
        [SerializeField] float blockedDrillVolume = 1f;
        [SerializeField] AudioSource getResourceAudio;
        [SerializeField] AudioClip getResourceClip;
        [SerializeField] float getResourceVolume = 1f;

        const float GarageDownAngle = 0f;
        const float GarageUpAngle = 180f;

        float _facingAngle;
        float _rotationTargetAngle;
        Vector3 _moveTarget;
        Tween _rotateTween;
        bool _isGarageShaftMove;
        bool _isDrilling;
        bool _isBlockedDrill;
        Vector2Int _drillFromCell;
        Vector2Int _drillTargetCell;
        float _drillProgress;
        Vector3 _drillStartPosition;
        float _moveSpeedMultiplier = 1f;
        bool _garageHomingRotation;

        public bool IsRotating => _rotateTween != null && _rotateTween.IsActive();
        public bool IsMoving { get; private set; }
        public bool IsDrilling => _isDrilling;
        public float DrillProgress => _drillProgress;

        void Awake()
        {
            _facingAngle = Mathf.DeltaAngle(0f, transform.eulerAngles.z);

            if (laserBeams == null)
            {
                laserBeams = GetComponent<DroneLaserBeams>();
            }

            if (depthWorld == null)
            {
                depthWorld = FindFirstObjectByType<DepthWorld>();
            }

            PrepareBlockedDrillAudio();
            PrepareGetResourceAudio();
        }

        void OnDestroy()
        {
            _rotateTween?.Kill();
        }

        void Update()
        {
            if (InputManager.Instance == null)
            {
                return;
            }

            var moveX = InputManager.Instance.MoveX;
            var moveY = InputManager.Instance.MoveY;
            var hasInput = moveX * moveX + moveY * moveY > 0.0001f;
            var inGarageApproach = IsInGarageApproach();
            var onGarageShaft = IsOnGarageShaft();
            var wantsGarageExit = WantsGarageExit(hasInput, moveX, moveY);
            UpdateGarageAutoPark(inGarageApproach, wantsGarageExit);

            if (_isBlockedDrill)
            {
                StepBlockedDrill(moveX, moveY, hasInput);
                return;
            }

            if (_isDrilling)
            {
                StepDrilling();
                return;
            }

            if (IsMoving)
            {
                StepMove();
                if (IsMoving)
                {
                    return;
                }
            }

            if (onGarageShaft)
            {
                if (IsRotating)
                {
                    return;
                }

                if (!IsFacing(GarageDownAngle))
                {
                    return;
                }

                if (!hasInput)
                {
                    return;
                }

                if (Mathf.Abs(moveY) <= Mathf.Abs(moveX))
                {
                    return;
                }

                TryStartGarageVerticalMove(moveY < 0f);
                return;
            }

            if (IsRotating)
            {
                if (hasInput)
                {
                    TryStartRotation(InputToAngle(moveX, moveY));
                }

                return;
            }

            if (!hasInput)
            {
                if (!_isDrilling && !_isBlockedDrill)
                {
                    laserBeams?.StopDrillSound();
                }

                return;
            }

            var targetAngle = InputToAngle(moveX, moveY);

            if (!IsFacing(targetAngle))
            {
                if (!_isDrilling && !_isBlockedDrill)
                {
                    laserBeams?.StopDrillSound();
                }

                TryStartRotation(targetAngle);
                return;
            }

            TryStartMoveOrDrill();
        }

        void TryStartMoveOrDrill()
        {
            if (WorldGrid.TryGetGarageSlot(transform.position, out var garageSlot))
            {
                TryStartGarageMove(garageSlot);
                return;
            }

            var currentCell = WorldGrid.WorldToCell(transform.position);
            if (!TryGetTargetCell(out var targetCell))
            {
                return;
            }

            _moveSpeedMultiplier = GetCurrentMagneticSpeedMultiplier();

            if (CanEnterCell(currentCell, targetCell))
            {
                laserBeams?.StopDrillSound();
                transform.position = WorldGrid.DroneCellToWorld(currentCell);
                _moveTarget = WorldGrid.DroneCellToWorld(targetCell);
                _isGarageShaftMove = false;
                IsMoving = true;
                return;
            }

            var world = GameManager.Instance?.World;
            if (!DrillRules.CanDrillCell(currentCell, targetCell, world))
            {
                if (GarageBounds.IsSurfaceProtected(targetCell.y))
                {
                    StartBlockedDrill(targetCell);
                }

                return;
            }

            var laserTier = GameManager.Instance?.Session?.LaserTier ?? 1;
            if (DrillRules.CanBreakCell(targetCell, world, laserTier))
            {
                TryStartDrill(targetCell, currentCell);
            }
            else
            {
                StartBlockedDrill(targetCell);
            }
        }

        void TryStartDrill(Vector2Int targetCell, Vector2Int fromCell)
        {
            _isDrilling = true;
            _drillFromCell = fromCell;
            _drillTargetCell = targetCell;
            _drillProgress = 0f;
            _drillStartPosition = CellAnchorWorld(fromCell);
            transform.position = _drillStartPosition;

            var speedMultiplier = GetCurrentMagneticSpeedMultiplier();
            _moveSpeedMultiplier = speedMultiplier;
            laserBeams?.BeginDrill(_facingAngle, drillDuration / Mathf.Max(speedMultiplier, 0.01f), false);
        }

        void StepDrilling()
        {
            var speedMultiplier = GetCurrentMagneticSpeedMultiplier();
            _moveSpeedMultiplier = speedMultiplier;
            _drillProgress += Time.deltaTime * speedMultiplier / drillDuration;
            var t = Mathf.Clamp01(_drillProgress);
            var targetWorld = WorldGrid.DroneCellToWorld(_drillTargetCell);
            transform.position = Vector3.Lerp(_drillStartPosition, targetWorld, t);

            laserBeams?.UpdateDrill(Time.deltaTime);
            if (depthWorld != null)
            {
                depthWorld.SetDrillClip(_drillTargetCell.x, _drillTargetCell.y, t, _facingAngle);
            }

            if (_drillProgress < 1f)
            {
                return;
            }

            CompleteDrill();
        }

        void CompleteDrill()
        {
            var world = GameManager.Instance?.World;
            var cell = _drillTargetCell;

            _isDrilling = false;
            _drillProgress = 0f;

            var hadResource = false;
            if (world != null)
            {
                var x = world.WrapX(cell.x);
                hadResource = world.GetCell(x, cell.y).HasResource;
            }

            if (DrillRules.TryClearCell(cell, world))
            {
                if (hadResource)
                {
                    PlayGetResourceSound();
                }

                if (depthWorld != null && world != null && GameManager.Instance != null)
                {
                    depthWorld.RefreshCell(world, GameManager.Instance.Assets, cell.x, cell.y);
                }
            }
            else
            {
                depthWorld?.ClearDrillClip(cell.x, cell.y);
            }

            transform.position = WorldGrid.DroneCellToWorld(cell);
        }

        void StartBlockedDrill(Vector2Int targetCell)
        {
            laserBeams?.StopDrillSound();
            _isBlockedDrill = true;
            _drillTargetCell = targetCell;
            PlayBlockedDrillSound();
        }

        void StepBlockedDrill(float moveX, float moveY, bool hasInput)
        {
            if (!hasInput || !IsFacing(InputToAngle(moveX, moveY)))
            {
                StopBlockedDrill();
                return;
            }

            var world = GameManager.Instance?.World;
            var currentCell = WorldGrid.WorldToCell(transform.position);
            if (!TryGetTargetCell(out var targetCell))
            {
                StopBlockedDrill();
                return;
            }

            if (GarageBounds.IsSurfaceProtected(targetCell.y))
            {
                return;
            }

            if (!DrillRules.CanDrillCell(currentCell, targetCell, world))
            {
                StopBlockedDrill();
                return;
            }

            var laserTier = GameManager.Instance?.Session?.LaserTier ?? 1;
            if (DrillRules.CanBreakCell(targetCell, world, laserTier))
            {
                StopBlockedDrill();
            }
        }

        void StopBlockedDrill()
        {
            _isBlockedDrill = false;
        }

        void PrepareBlockedDrillAudio()
        {
            if (blockedDrillAudio == null)
            {
                return;
            }

            blockedDrillAudio.playOnAwake = false;
            blockedDrillAudio.loop = false;
            blockedDrillAudio.spatialBlend = 0f;
        }

        void PlayBlockedDrillSound()
        {
            if (blockedDrillAudio == null || blockedDrillClip == null)
            {
                return;
            }

            blockedDrillAudio.PlayOneShot(blockedDrillClip, blockedDrillVolume);
        }

        void PrepareGetResourceAudio()
        {
            if (getResourceAudio == null)
            {
                return;
            }

            getResourceAudio.playOnAwake = false;
            getResourceAudio.loop = false;
            getResourceAudio.spatialBlend = 0f;
        }

        void PlayGetResourceSound()
        {
            if (getResourceAudio == null || getResourceClip == null)
            {
                return;
            }

            getResourceAudio.PlayOneShot(getResourceClip, getResourceVolume);
        }

        static Vector3 CellAnchorWorld(Vector2Int cell)
        {
            if (cell == GarageBounds.SpawnCell)
            {
                return WorldGrid.GarageSpawnWorld();
            }

            if (cell == GarageBounds.SecondCell)
            {
                return WorldGrid.GarageSecondWorld();
            }

            return WorldGrid.DroneCellToWorld(cell);
        }

        void UpdateGarageAutoPark(bool inApproach, bool wantsGarageExit)
        {
            if (!inApproach || wantsGarageExit || _isDrilling || _isBlockedDrill || IsMoving || IsRotating)
            {
                return;
            }

            var cell = WorldGrid.WorldToCell(transform.position);
            var world = GameManager.Instance?.World;
            if (world != null)
            {
                cell.x = world.WrapX(cell.x);
            }

            if (cell.y > GarageBounds.SpawnCell.y)
            {
                TryStartParkingMoveStep(cell);
                return;
            }

            if (!IsAtGarageSpawn())
            {
                transform.position = WorldGrid.GarageSpawnWorld();
            }

            if (!IsFacing(GarageDownAngle) && !IsGarageHomingToDown())
            {
                TryStartGarageHomingRotation();
            }
        }

        static bool WantsGarageExit(bool hasInput, float moveX, float moveY)
        {
            if (!hasInput || Mathf.Abs(moveY) <= Mathf.Abs(moveX))
            {
                return false;
            }

            return moveY < 0f;
        }

        void TryStartParkingMoveStep(Vector2Int cell)
        {
            if (cell == GarageBounds.ExitCell)
            {
                laserBeams?.StopDrillSound();
                transform.position = WorldGrid.DroneCellToWorld(GarageBounds.ExitCell);
                StartParkingMove(WorldGrid.GarageSecondWorld());
                return;
            }

            if (cell == GarageBounds.SecondCell)
            {
                laserBeams?.StopDrillSound();
                transform.position = WorldGrid.GarageSecondWorld();
                StartParkingMove(WorldGrid.GarageSpawnWorld());
            }
        }

        void StartParkingMove(Vector3 target)
        {
            _moveTarget = target;
            _isGarageShaftMove = true;
            IsMoving = true;
        }

        static bool IsAtGarageSpawn(Vector3 position)
        {
            var spawn = WorldGrid.GarageSpawnWorld();
            return (position - spawn).sqrMagnitude < 0.0001f;
        }

        bool IsAtGarageSpawn() => IsAtGarageSpawn(transform.position);

        bool IsGarageHomingToDown()
        {
            return _garageHomingRotation
                && IsRotating
                && Mathf.Abs(Mathf.DeltaAngle(_rotationTargetAngle, GarageDownAngle)) < 0.01f;
        }

        void TryStartGarageHomingRotation()
        {
            if (IsFacing(GarageDownAngle) || IsGarageHomingToDown())
            {
                return;
            }

            _rotateTween?.Kill();
            laserBeams?.ClearDrillParticles();
            _garageHomingRotation = true;
            _rotationTargetAngle = GarageDownAngle;

            var currentVisual = transform.eulerAngles.z;
            var delta = Mathf.Abs(Mathf.DeltaAngle(currentVisual, GarageDownAngle));
            var duration = delta / 360f * fullRotationDuration;

            _rotateTween = transform
                .DORotate(new Vector3(0f, 0f, GarageDownAngle), duration, RotateMode.Fast)
                .SetEase(Ease.Linear)
                .OnComplete(() =>
                {
                    SnapFacing(GarageDownAngle);
                    _garageHomingRotation = false;
                });
        }

        void TryStartGarageVerticalMove(bool wantDown)
        {
            if (!TryGetGarageShaftSlot(out var slot))
            {
                return;
            }

            if (wantDown)
            {
                TryStartGarageMoveDown(slot);
                return;
            }

            TryStartGarageMoveUp(slot);
        }

        bool TryGetGarageShaftSlot(out WorldGrid.GarageSlot slot)
        {
            slot = default;
            var cell = WorldGrid.WorldToCell(transform.position);
            var world = GameManager.Instance?.World;
            if (world != null)
            {
                cell.x = world.WrapX(cell.x);
            }

            if (cell == GarageBounds.SpawnCell)
            {
                slot = WorldGrid.GarageSlot.Spawn;
                return true;
            }

            if (cell == GarageBounds.SecondCell)
            {
                slot = WorldGrid.GarageSlot.Second;
                return true;
            }

            return false;
        }

        void TryStartGarageMoveDown(WorldGrid.GarageSlot slot)
        {
            if (slot == WorldGrid.GarageSlot.Spawn)
            {
                transform.position = WorldGrid.GarageSpawnWorld();
                _moveTarget = WorldGrid.GarageSecondWorld();
                _isGarageShaftMove = true;
                IsMoving = true;
                return;
            }

            if (slot != WorldGrid.GarageSlot.Second)
            {
                return;
            }

            if (CanEnterCell(GarageBounds.SecondCell, GarageBounds.ExitCell))
            {
                transform.position = WorldGrid.GarageSecondWorld();
                _moveTarget = WorldGrid.DroneCellToWorld(GarageBounds.ExitCell);
                _isGarageShaftMove = false;
                IsMoving = true;
                return;
            }

            var world = GameManager.Instance?.World;
            if (DrillRules.CanDrillCell(GarageBounds.SecondCell, GarageBounds.ExitCell, world))
            {
                TryStartDrill(GarageBounds.ExitCell, GarageBounds.SecondCell);
            }
        }

        void TryStartGarageMoveUp(WorldGrid.GarageSlot slot)
        {
            if (slot != WorldGrid.GarageSlot.Second)
            {
                return;
            }

            transform.position = WorldGrid.GarageSecondWorld();
            _moveTarget = WorldGrid.GarageSpawnWorld();
            _isGarageShaftMove = true;
            IsMoving = true;
        }

        void TryStartGarageMove(WorldGrid.GarageSlot slot)
        {
            if (IsFacing(GarageDownAngle))
            {
                TryStartGarageMoveDown(slot);
                return;
            }

            if (IsFacing(GarageUpAngle))
            {
                TryStartGarageMoveUp(slot);
            }
        }

        void StepMove()
        {
            _moveSpeedMultiplier = GetCurrentMagneticSpeedMultiplier();

            var speed = (_isGarageShaftMove ? garageMoveSpeed : moveSpeed) * _moveSpeedMultiplier;
            var position = transform.position;
            var next = Vector3.MoveTowards(position, _moveTarget, speed * Time.deltaTime);
            transform.position = next;

            if ((next - _moveTarget).sqrMagnitude > 0.000001f)
            {
                return;
            }

            transform.position = _moveTarget;
            _isGarageShaftMove = false;
            IsMoving = false;
        }

        float GetCurrentMagneticSpeedMultiplier()
        {
            if (TryGetMagneticEffect(_facingAngle, out var effect, out var anomaly))
            {
                return MagneticFieldQuery.GetSpeedMultiplier(anomaly, effect);
            }

            return 1f;
        }

        bool TryGetMagneticEffect(
            float facingAngle,
            out MagneticMoveEffect effect,
            out MagneticAnomaly anomaly)
        {
            effect = MagneticMoveEffect.None;
            anomaly = null;

            if (!TryGetMagneticAnomalyAtDrone(out anomaly))
            {
                return false;
            }

            effect = MagneticFieldQuery.GetMoveEffect(anomaly, facingAngle);
            return effect != MagneticMoveEffect.None;
        }

        bool TryGetMagneticAnomalyAtDrone(out MagneticAnomaly anomaly)
        {
            anomaly = null;

            var gameManager = GameManager.Instance;
            var world = gameManager?.World;
            var magneticWorld = gameManager?.MagneticAnomalyWorld;
            if (world == null || magneticWorld == null)
            {
                return false;
            }

            var cell = WorldGrid.WorldToCell(transform.position);
            cell.x = world.WrapX(cell.x);
            const int ensurePadding = 220;
            magneticWorld.EnsureInRect(
                cell.x - ensurePadding,
                cell.x + ensurePadding,
                cell.y - ensurePadding,
                cell.y + ensurePadding);

            return magneticWorld.TryGetAtWorld(transform.position, out anomaly);
        }

        bool TryGetTargetCell(out Vector2Int targetCell)
        {
            targetCell = WorldGrid.GetFacingCell(transform.position, _facingAngle);
            return true;
        }

        bool CanMoveInDirection(float angle)
        {
            if (WorldGrid.TryGetGarageSlot(transform.position, out var slot))
            {
                return CanGarageMove(slot, angle);
            }

            var from = WorldGrid.WorldToCell(transform.position);
            var to = WorldGrid.GetFacingCell(transform.position, angle);
            return CanEnterCell(from, to);
        }

        static bool CanGarageMove(WorldGrid.GarageSlot slot, float angle)
        {
            if (Mathf.Abs(Mathf.DeltaAngle(angle, GarageUpAngle)) < 0.01f)
            {
                return slot == WorldGrid.GarageSlot.Second;
            }

            if (Mathf.Abs(Mathf.DeltaAngle(angle, GarageDownAngle)) < 0.01f)
            {
                if (slot == WorldGrid.GarageSlot.Spawn)
                {
                    return true;
                }

                if (slot == WorldGrid.GarageSlot.Second)
                {
                    var world = GameManager.Instance?.World;
                    if (world == null)
                    {
                        return false;
                    }

                    var to = GarageBounds.ExitCell;
                    if (to.y < 0 || to.y > world.WorldRadius)
                    {
                        return false;
                    }

                    var x = world.WrapX(to.x);
                    if (!world.GetCell(x, to.y).IsSolid)
                    {
                        return true;
                    }

                    return DrillRules.CanDrillCell(GarageBounds.SecondCell, to, world);
                }
            }

            return false;
        }

        bool CanEnterCell(Vector2Int from, Vector2Int to)
        {
            if (!GarageBounds.CanMoveTo(to.x, to.y) || !GarageBounds.AllowsMove(from, to))
            {
                return false;
            }

            var world = GameManager.Instance?.World;
            if (world == null)
            {
                return false;
            }

            if (to.y < 0 || to.y > world.WorldRadius)
            {
                return false;
            }

            var x = world.WrapX(to.x);
            return !world.GetCell(x, to.y).IsSolid;
        }

        void TryStartRotation(float targetAngle)
        {
            if (_isDrilling || IsOnGarageShaft())
            {
                return;
            }

            if (IsRotating && Mathf.Abs(Mathf.DeltaAngle(_rotationTargetAngle, targetAngle)) < 0.01f)
            {
                return;
            }

            if (!IsRotating && IsFacing(targetAngle))
            {
                return;
            }

            _rotateTween?.Kill();

            laserBeams?.ClearDrillParticles();
            _rotationTargetAngle = targetAngle;

            var currentVisual = transform.eulerAngles.z;
            var delta = Mathf.Abs(Mathf.DeltaAngle(currentVisual, targetAngle));
            var duration = delta / 360f * fullRotationDuration;

            _rotateTween = transform
                .DORotate(new Vector3(0f, 0f, targetAngle), duration, RotateMode.Fast)
                .SetEase(Ease.Linear)
                .OnComplete(() => SnapFacing(targetAngle));
        }

        bool IsInGarageApproach()
        {
            var cell = WorldGrid.WorldToCell(transform.position);
            return cell.x == GarageBounds.ShaftX
                && cell.y >= GarageBounds.SpawnCell.y
                && cell.y <= GarageBounds.ExitCell.y;
        }

        bool IsOnGarageShaft()
        {
            var cell = WorldGrid.WorldToCell(transform.position);
            return GarageBounds.IsShaft(cell.x, cell.y);
        }

        void SnapFacing(float facingAngle)
        {
            _facingAngle = facingAngle;
            transform.rotation = Quaternion.Euler(0f, 0f, facingAngle);
            _rotateTween = null;
        }

        bool IsFacing(float targetAngle)
        {
            return Mathf.Abs(Mathf.DeltaAngle(_facingAngle, targetAngle)) < 0.01f;
        }

        static float InputToAngle(float x, float y)
        {
            if (Mathf.Abs(x) > Mathf.Abs(y))
            {
                return x > 0f ? 90f : -90f;
            }

            return y > 0f ? 180f : 0f;
        }
    }
}
