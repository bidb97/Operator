using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Operator.Managers
{
    public enum MoveDirection
    {
        Up,
        Down,
        Left,
        Right
    }

    public class InputManager : MonoBehaviour
    {
        public static InputManager Instance { get; private set; }

        public event Action<MoveDirection> OnMovePressed;

        /// <summary>-1…1. Удержание D-pad или клавиатура.</summary>
        public float MoveX { get; private set; }

        public float MoveY { get; private set; }

        [SerializeField] InputActionAsset inputActions;

        InputAction _moveAction;

        bool _uiHoldLeft;
        bool _uiHoldRight;
        bool _uiHoldUp;
        bool _uiHoldDown;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            var playerMap = inputActions.FindActionMap("Player");
            _moveAction = playerMap.FindAction("Move");
        }

        void OnEnable()
        {
            _moveAction?.Enable();
        }

        void OnDisable()
        {
            _moveAction?.Disable();
        }

        void Update()
        {
            MoveX = ReadMoveX();
            MoveY = ReadMoveY();

            if (_moveAction == null || !_moveAction.WasPressedThisFrame())
            {
                return;
            }

            var v = _moveAction.ReadValue<Vector2>();
            var dir = VectorToDirection(v);

            if (dir.HasValue)
            {
                RaiseMove(dir.Value);
            }
        }

        public void SetUiHoldLeft(bool held) => _uiHoldLeft = held;

        public void SetUiHoldRight(bool held) => _uiHoldRight = held;

        public void SetUiHoldUp(bool held) => _uiHoldUp = held;

        public void SetUiHoldDown(bool held) => _uiHoldDown = held;

        public void MoveUp() => RaiseMove(MoveDirection.Up);
        public void MoveDown() => RaiseMove(MoveDirection.Down);
        public void MoveLeft() => RaiseMove(MoveDirection.Left);
        public void MoveRight() => RaiseMove(MoveDirection.Right);

        void RaiseMove(MoveDirection dir) => OnMovePressed?.Invoke(dir);

        float ReadMoveX()
        {
            if (_uiHoldLeft)
            {
                return -1f;
            }

            if (_uiHoldRight)
            {
                return 1f;
            }

            if (_moveAction == null)
            {
                return 0f;
            }

            return Mathf.Clamp(_moveAction.ReadValue<Vector2>().x, -1f, 1f);
        }

        float ReadMoveY()
        {
            if (_uiHoldUp)
            {
                return 1f;
            }

            if (_uiHoldDown)
            {
                return -1f;
            }

            if (_moveAction == null)
            {
                return 0f;
            }

            return Mathf.Clamp(_moveAction.ReadValue<Vector2>().y, -1f, 1f);
        }

        static MoveDirection? VectorToDirection(Vector2 v)
        {
            if (Mathf.Abs(v.x) > Mathf.Abs(v.y))
            {
                return v.x > 0f ? MoveDirection.Right : MoveDirection.Left;
            }

            if (Mathf.Abs(v.y) > 0.01f)
            {
                return v.y > 0f ? MoveDirection.Up : MoveDirection.Down;
            }

            return null;
        }

        public void SetUiMove(float x, float y)
        {
            _uiHoldLeft = x < 0f;
            _uiHoldRight = x > 0f;
            _uiHoldUp = y > 0f;
            _uiHoldDown = y < 0f;
        }

    }
}
