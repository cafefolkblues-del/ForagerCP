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
        public event Action DashPressed;

        /// 핫바에서 고른 것을 커서가 가리키는 칸에 놓는다.
        public event Action PlacePressed;

        /// 핫바 슬롯 직접 선택(0부터). 숫자키 1~9.
        public event Action<int> HotbarSelected;

        /// 휠로 한 칸씩 이동(+1 / -1).
        public event Action<int> HotbarScrolled;

        /// 좌클릭을 뗀 순간. 인자는 누르고 있던 시간(차지 판정용).
        public event Action<float> AttackReleased;

        InputAction _move;
        InputAction _attack;
        InputAction _shield;
        InputAction _dash;
        InputAction _interact;
        InputAction _inventory;
        InputAction _map;
        InputAction _cancel;
        InputAction _place;

        /// 창이 떠 있는 동안 참. UiRouter가 밀어 넣는다 —
        /// 입력이 UI를 참조하면 입력만 떼어 시험할 수가 없어진다.
        ///
        /// 휠이 핫바로 새는 것만 막아서는 부족하다. 상점에서 버튼을 눌러도 칼이 나가고
        /// WASD로 걸어다니므로, 게임 쪽 입력을 한 자리에서 통째로 끊는다.
        public bool GameplayBlocked { get; set; }

        public Vector2 MoveAxis => GameplayBlocked ? Vector2.zero : _move.ReadValue<Vector2>();

        /// 채집은 누르고 있는 동안 쿨다운 간격으로 반복되게 둔다(6타 연타 피로 감소).
        /// 1타당 판정은 PlayerAttacker의 쿨다운이 담당.
        public bool AttackHeld => !GameplayBlocked && _attack.IsPressed();

        /// 방패는 누르고 있는 동안만 유지(토글 아님).
        public bool ShieldHeld => !GameplayBlocked && _shield.IsPressed();

        /// 좌클릭을 누르고 있는 시간. 떼면 0으로 돌아간다. 차지 게이지가 이 값을 본다.
        public float AttackHoldTime { get; private set; }

        /// Pointer.current: 마우스 없는 환경(패드 전용)에서 null이 나오므로 호출부가 아니라 여기서 막는다.
        public Vector2 PointerPosition => Pointer.current != null ? Pointer.current.position.ReadValue() : Vector2.zero;

        bool _wasAttackHeld;

        void Awake()
        {
            _move = new InputAction("Move", InputActionType.Value);
            _move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");

            _attack = new InputAction("Attack", InputActionType.Button, "<Mouse>/leftButton");
            _shield = new InputAction("Shield", InputActionType.Button, "<Mouse>/rightButton");
            _dash = new InputAction("Dash", InputActionType.Button, "<Keyboard>/leftShift");
            _interact = new InputAction("Interact", InputActionType.Button, "<Keyboard>/f");
            _inventory = new InputAction("Inventory", InputActionType.Button, "<Keyboard>/i");
            _map = new InputAction("Map", InputActionType.Button, "<Keyboard>/m");
            _cancel = new InputAction("Cancel", InputActionType.Button, "<Keyboard>/escape");
            _place = new InputAction("Place", InputActionType.Button, "<Keyboard>/e");

            _interact.performed += OnInteractPerformed;
            _inventory.performed += OnInventoryPerformed;
            _map.performed += OnMapPerformed;
            _cancel.performed += OnCancelPerformed;
            _dash.performed += OnDashPerformed;
            _place.performed += OnPlacePerformed;
        }

        void OnEnable()
        {
            _move.Enable();
            _attack.Enable();
            _shield.Enable();
            _dash.Enable();
            _interact.Enable();
            _inventory.Enable();
            _map.Enable();
            _cancel.Enable();
            _place.Enable();
        }

        void OnDisable()
        {
            _move.Disable();
            _attack.Disable();
            _shield.Disable();
            _dash.Disable();
            _interact.Disable();
            _inventory.Disable();
            _map.Disable();
            _cancel.Disable();
            _place.Disable();
        }

        /// 홀드 시간과 릴리즈 시점은 여기 한 곳에서만 재고, 쓰는 쪽은 결과만 받는다.
        /// 컴포넌트마다 직접 재면 프레임 순서에 따라 값이 어긋난다.
        void Update()
        {
            bool held = AttackHeld;

            if (held) AttackHoldTime += Time.deltaTime;
            else if (_wasAttackHeld)
            {
                AttackReleased?.Invoke(AttackHoldTime);
                AttackHoldTime = 0f;
            }

            _wasAttackHeld = held;

            ReadHotbar();
        }

        /// 숫자키 9개를 InputAction으로 만들면 선언만 아홉 줄이 되고, 어느 키가 눌렸는지
        /// 다시 역추적해야 한다. 키 배열을 직접 읽는 편이 짧고 의도도 분명하다.
        void ReadHotbar()
        {
            if (GameplayBlocked) return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                for (int i = 0; i < HotbarKeys.Length; i++)
                {
                    if (keyboard[HotbarKeys[i]].wasPressedThisFrame) HotbarSelected?.Invoke(i);
                }
            }

            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) < 0.01f) return;

            // 휠은 위로 굴리면 앞 칸으로 — 마인크래프트와 같은 방향.
            HotbarScrolled?.Invoke(scroll > 0f ? -1 : 1);
        }

        static readonly Key[] HotbarKeys =
        {
            Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5,
            Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9
        };

        void OnDestroy()
        {
            _interact.performed -= OnInteractPerformed;
            _inventory.performed -= OnInventoryPerformed;
            _map.performed -= OnMapPerformed;
            _cancel.performed -= OnCancelPerformed;
            _dash.performed -= OnDashPerformed;
            _place.performed -= OnPlacePerformed;

            _move.Dispose();
            _attack.Dispose();
            _shield.Dispose();
            _dash.Dispose();
            _interact.Dispose();
            _inventory.Dispose();
            _map.Dispose();
            _cancel.Dispose();
            _place.Dispose();
        }

        /// ESC와 가방 열기는 막지 않는다 — 창을 닫을 길까지 끊으면 빠져나올 수가 없다.
        void OnInteractPerformed(InputAction.CallbackContext _)
        {
            if (!GameplayBlocked) InteractPressed?.Invoke();
        }
        void OnInventoryPerformed(InputAction.CallbackContext _) => InventoryToggled?.Invoke();
        void OnMapPerformed(InputAction.CallbackContext _) => MapToggled?.Invoke();
        void OnCancelPerformed(InputAction.CallbackContext _) => CancelPressed?.Invoke();
        void OnDashPerformed(InputAction.CallbackContext _)
        {
            if (!GameplayBlocked) DashPressed?.Invoke();
        }
        void OnPlacePerformed(InputAction.CallbackContext _)
        {
            if (!GameplayBlocked) PlacePressed?.Invoke();
        }
    }
}
