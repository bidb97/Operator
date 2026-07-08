using Operator.Depth.Core;
using UnityEngine;

namespace Operator.Bootstrap
{
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] float drillStartUnityY = -1f;
        [SerializeField] int followStartCellY = 5;

        bool _followingVertical;
        float _frozenRigY;
        float _followAnchorDroneY;
        float _followAnchorRigY;

        public void SetTarget(Transform value) => target = value;

        public void SyncPosition()
        {
            if (target == null)
            {
                return;
            }

            ApplyPosition(target.position, forceSnap: true);
        }

        void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            ApplyPosition(target.position, forceSnap: false);
        }

        void ApplyPosition(Vector3 targetPosition, bool forceSnap)
        {
            var followY = ComputeFollowY(targetPosition.y);
            var cell = WorldGrid.WorldToCell(targetPosition);
            var shouldFollow = cell.y >= followStartCellY;

            if (forceSnap)
            {
                if (shouldFollow)
                {
                    BeginFollow(targetPosition.y, followY);
                }
                else
                {
                    _followingVertical = false;
                    _frozenRigY = followY;
                }
            }
            else if (shouldFollow && !_followingVertical)
            {
                BeginFollow(targetPosition.y, _frozenRigY);
            }
            else if (!shouldFollow && _followingVertical)
            {
                _frozenRigY = transform.position.y;
                _followingVertical = false;
            }

            var rigY = _followingVertical
                ? _followAnchorRigY + (targetPosition.y - _followAnchorDroneY)
                : _frozenRigY;

            transform.position = new Vector3(targetPosition.x, rigY, transform.position.z);
        }

        void BeginFollow(float droneWorldY, float rigWorldY)
        {
            _followingVertical = true;
            _followAnchorDroneY = droneWorldY;
            _followAnchorRigY = rigWorldY;
        }

        float ComputeFollowY(float targetWorldY)
        {
            var cam = Camera.main;
            var orthoSize = cam != null ? cam.orthographicSize : 10f;
            var verticalOffset = -orthoSize / 3f - drillStartUnityY;

            return targetWorldY + verticalOffset;
        }
    }
}
