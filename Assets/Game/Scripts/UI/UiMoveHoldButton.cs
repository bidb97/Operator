using Operator.Managers;
using UnityEngine;
using UnityEngine.EventSystems;
using MoveDirection = Operator.Managers.MoveDirection;

namespace Operator.UI
{
    public class UiMoveHoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] MoveDirection direction;

        public void OnPointerDown(PointerEventData eventData) => SetHeld(true);

        public void OnPointerUp(PointerEventData eventData) => SetHeld(false);

        public void OnPointerExit(PointerEventData eventData) => SetHeld(false);

        void SetHeld(bool held)
        {
            if (InputManager.Instance == null)
            {
                return;
            }

            switch (direction)
            {
                case MoveDirection.Left:
                    InputManager.Instance.SetUiHoldLeft(held);
                    break;
                case MoveDirection.Right:
                    InputManager.Instance.SetUiHoldRight(held);
                    break;
                case MoveDirection.Up:
                    InputManager.Instance.SetUiHoldUp(held);
                    break;
                case MoveDirection.Down:
                    InputManager.Instance.SetUiHoldDown(held);
                    break;
            }
        }
    }
}
