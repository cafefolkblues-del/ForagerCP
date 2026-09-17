using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ForagerCP
{
    /// 사양 1-2의 키 배치를 코드로 정의한다.
    /// 템플릿 InputSystem_Actions 에셋은 Interact가 E로 잡혀 있어서, 에셋을 고치는 대신
    /// 액션을 코드에서 만들어 사양(F)과 어긋나지 않게 한다.
    public class GameInput : MonoBehaviour
    {
        public event Action InteractPressed;
        public event Action InventoryToggled;
        public event Action MapToggled;
        public event Action CancelPressed;

        InputAction _move;
        InputAction _attack;
        InputAction _interact;
        InputAction _inventory;
        InputAction _map;
        InputAction _cancel;

        public Vector2 MoveAxis => _move.ReadValue<Vector2>();

        /// 채집은 누르고 있는 동안 쿨다운 간격으로 반복되게 둔다(6타 연타 피로 감소).
        /// 1타당 판정은 PlayerHarvester의 쿨다운이 담당.
        public bool AttackHeld => _attack.IsPressed();

        /// Pointer.current: 마우스 없는 환경(패드 전용)에서 null이 나오므로 호출부가 아니라 여기서 막는다.
        public Vector2 PointerPosition => Pointer.current != null ? Pointer.current.position.ReadValue() : Vector2.zero;

        void Awake()
        {
            _move = new InputAction("Move", InputActionType.Value);
            _move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");

            _attack = new InputAction("Attack", InputActionType.Button, "<Mouse>/leftButton");
            _interact = new InputAction("Interact", InputActionType.Button, "<Keyboard>/f");
            _inventory = new InputAction("Inventory", InputActionType.Button, "<Keyboard>/i");
            _map = new InputAction("Map", InputActionType.Button, "<Keyboard>/m");
            _cancel = new InputAction("Cancel", InputActionType.Button, "<Keyboard>/escape");

            _interact.performed += OnInteractPerformed;
            _inventory.performed += OnInventoryPerformed;
            _map.performed += OnMapPerformed;
            _cancel.performed += OnCancelPerformed;
        }

        void OnEnable()
        {
            _move.Enable();
            _attack.Enable();
            _interact.Enable();
            _inventory.Enable();
            _map.Enable();
            _cancel.Enable();
        }

        void OnDisable()
        {
            _move.Disable();
            _attack.Disable();
            _interact.Disable();
            _inventory.Disable();
            _map.Disable();
            _cancel.Disable();
        }

        void OnDestroy()
        {
            _interact.performed -= OnInteractPerformed;
            _inventory.performed -= OnInventoryPerformed;
            _map.performed -= OnMapPerformed;
            _cancel.performed -= OnCancelPerformed;

            _move.Dispose();
            _attack.Dispose();
            _interact.Dispose();
            _inventory.Dispose();
            _map.Dispose();
            _cancel.Dispose();
        }

        void OnInteractPerformed(InputAction.CallbackContext _) => InteractPressed?.Invoke();
        void OnInventoryPerformed(InputAction.CallbackContext _) => InventoryToggled?.Invoke();
        void OnMapPerformed(InputAction.CallbackContext _) => MapToggled?.Invoke();
        void OnCancelPerformed(InputAction.CallbackContext _) => CancelPressed?.Invoke();
    }
}
