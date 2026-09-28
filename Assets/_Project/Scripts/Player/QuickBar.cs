using System;
using UnityEngine;

namespace ForagerCP
{
    /// 화면 하단 핫바의 '상태'. 무엇이 선택돼 있는지만 들고 있고 그리지는 않는다.
    ///
    /// 표시는 QuickBarView가, 설치는 다음에 붙을 설치 컴포넌트가 이 값을 읽어간다.
    /// 선택 상태를 UI 안에 두면 설치 쪽이 UI를 참조하게 되고, UI를 갈아끼울 때 게임 로직이 따라 깨진다.
    public class QuickBar : MonoBehaviour
    {
        [SerializeField] GameInput _input;
        [SerializeField] Inventory _inventory;

        /// 핫바에 보여줄 칸 수. 인벤토리 앞쪽 칸을 그대로 쓴다.
        [SerializeField] int _slotCount = 9;

        public event Action<QuickBar> Changed;

        public int SlotCount => Mathf.Max(1, _slotCount);
        public int SelectedIndex { get; private set; }

        /// 지금 선택된 칸의 아이템. 빈 칸이면 null — 설치 쪽은 이 값만 보면 된다.
        public ItemDefinition SelectedItem
        {
            get
            {
                if (_inventory == null) return null;
                if (SelectedIndex < 0 || SelectedIndex >= _inventory.Stacks.Count) return null;
                return _inventory.Stacks[SelectedIndex].Definition;
            }
        }

        public int SelectedCount
        {
            get
            {
                if (_inventory == null) return 0;
                if (SelectedIndex < 0 || SelectedIndex >= _inventory.Stacks.Count) return 0;
                return _inventory.Stacks[SelectedIndex].Count;
            }
        }

        public ItemStack StackAt(int index)
        {
            if (_inventory == null) return null;
            if (index < 0 || index >= _inventory.Stacks.Count) return null;
            return _inventory.Stacks[index];
        }

        void Awake()
        {
            if (_input == null) _input = FindFirstObjectByType<GameInput>();
            if (_inventory == null) _inventory = FindFirstObjectByType<Inventory>();
        }

        void OnEnable()
        {
            if (_input != null)
            {
                _input.HotbarSelected += Select;
                _input.HotbarScrolled += Move;
            }

            // 칸이 비거나 채워지면 표시도 따라가야 한다.
            if (_inventory != null) _inventory.Changed += OnInventoryChanged;
        }

        void OnDisable()
        {
            if (_input != null)
            {
                _input.HotbarSelected -= Select;
                _input.HotbarScrolled -= Move;
            }

            if (_inventory != null) _inventory.Changed -= OnInventoryChanged;
        }

        void Start() => Changed?.Invoke(this);

        void OnInventoryChanged(Inventory inventory) => Changed?.Invoke(this);

        public void Select(int index)
        {
            int clamped = Mathf.Clamp(index, 0, SlotCount - 1);
            if (clamped == SelectedIndex) return;

            SelectedIndex = clamped;
            Changed?.Invoke(this);
        }

        /// 끝에서 넘어가면 반대편으로 돌아온다. 마인크래프트와 같은 감각.
        public void Move(int delta)
        {
            if (delta == 0) return;

            int next = (SelectedIndex + delta) % SlotCount;
            if (next < 0) next += SlotCount;

            SelectedIndex = next;
            Changed?.Invoke(this);
        }
    }
}
