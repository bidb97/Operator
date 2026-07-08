using DG.Tweening;
using Operator.Managers;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Operator.UI
{
    public class UiDirectionalJoystick : MonoBehaviour,
        IPointerDownHandler, IDragHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] RectTransform handle;
        [SerializeField] float handleTravel = 50f;
        [SerializeField] float deadZone = 20f;
        [SerializeField] float activationThreshold = 45f;
        [SerializeField] float snapDuration = 0.12f;

        RectTransform _pad;
        Tweener _handleTween;

        void Awake()
        {
            _pad = (RectTransform)transform;
        }

        public void OnPointerDown(PointerEventData eventData) => ApplyPointer(eventData);

        public void OnDrag(PointerEventData eventData) => ApplyPointer(eventData);

        public void OnPointerUp(PointerEventData eventData) => Release();

        public void OnPointerExit(PointerEventData eventData) => Release();

        void ApplyPointer(PointerEventData eventData)
        {
            if (handle == null || InputManager.Instance == null)
            {
                return;
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _pad, eventData.position, eventData.pressEventCamera, out var local);

            if (local.magnitude < deadZone)
            {
                InputManager.Instance.SetUiMove(0f, 0f);
                MoveHandle(Vector2.zero);
                return;
            }

            float moveX = 0f;
            float moveY = 0f;
            var offset = Vector2.zero;

            if (Mathf.Abs(local.x) > Mathf.Abs(local.y))
            {
                if (Mathf.Abs(local.x) < activationThreshold)
                {
                    InputManager.Instance.SetUiMove(0f, 0f);
                    MoveHandle(Vector2.zero);
                    return;
                }

                moveX = local.x > 0f ? 1f : -1f;
                offset.x = moveX * handleTravel;
            }
            else
            {
                if (Mathf.Abs(local.y) < activationThreshold)
                {
                    InputManager.Instance.SetUiMove(0f, 0f);
                    MoveHandle(Vector2.zero);
                    return;
                }

                moveY = local.y > 0f ? 1f : -1f;
                offset.y = moveY * handleTravel;
            }

            InputManager.Instance.SetUiMove(moveX, moveY);
            MoveHandle(offset);
        }

        void Release()
        {
            InputManager.Instance?.SetUiMove(0f, 0f);
            MoveHandle(Vector2.zero);
        }

        void MoveHandle(Vector2 anchoredPosition)
        {
            _handleTween?.Kill();
            _handleTween = handle
                .DOAnchorPos(anchoredPosition, snapDuration)
                .SetEase(Ease.OutQuad);
        }
    }
}