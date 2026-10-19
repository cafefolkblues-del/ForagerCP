using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ForagerCP
{
    /// 보관함 화면. 격자 둘을 나란히 놓고, 칸을 누르면 반대쪽으로 옮긴다.
    ///
    /// 끌어놓기(드래그)를 넣지 않은 이유: 한 칸이 곧 한 종류이고 기획이 요구한 것은
    /// '넣고 꺼내기'다. 누르면 그 종류가 통째로 넘어가므로 왕복이 가장 적다.
    /// 수량을 골라 옮기는 것은 필요해지면 줄 UI(판매 화면)를 그대로 가져오면 된다.
    ///
    /// 옮기는 규칙 자체는 ItemContainer.MoveTo가 들고 있다 — 화면이 규칙을 들면
    /// 다른 화면이 생길 때 같은 규칙을 다시 쓰게 된다.
    public class StoragePanelView : MonoBehaviour
    {
        [SerializeField] UiWindow _window;
        [SerializeField] UiRouter _router;
        [SerializeField] Inventory _inventory;

        [Header("보관함 쪽")]
        [SerializeField] Transform _boxSlotParent;
        [SerializeField] TMP_Text _boxTitle;

        [Header("가방 쪽")]
        [SerializeField] Transform _bagSlotParent;
        [SerializeField] TMP_Text _bagTitle;

        [Header("공통")]
        [SerializeField] InventorySlotView _slotTemplate;
        [SerializeField] TMP_Text _notice;

        readonly List<InventorySlotView> _boxSlots = new List<InventorySlotView>();
        readonly List<InventorySlotView> _bagSlots = new List<InventorySlotView>();

        StorageBox _box;

        void Awake()
        {
            if (_window == null) _window = GetComponent<UiWindow>();
            if (_router == null) _router = FindFirstObjectByType<UiRouter>();
            if (_inventory == null) _inventory = FindFirstObjectByType<Inventory>();
            if (_slotTemplate != null) _slotTemplate.gameObject.SetActive(false);
        }

        void OnEnable()
        {
            // 보관함은 플레이 중에 생긴다(설치·세이브 복원). 그래서 지금 있는 것만 붙이면
            // 나중에 놓은 보관함이 화면을 못 띄운다 — 생길 때마다 붙여야 한다.
            foreach (StorageBox box in FindObjectsByType<StorageBox>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                box.OpenRequested += Open;
            }

            foreach (PlacedBlocks blocks in FindObjectsByType<PlacedBlocks>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                blocks.Created += OnBlockCreated;
            }

            if (_inventory != null) _inventory.Changed += OnInventoryChanged;
        }

        void OnDisable()
        {
            foreach (StorageBox box in FindObjectsByType<StorageBox>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                box.OpenRequested -= Open;
            }

            foreach (PlacedBlocks blocks in FindObjectsByType<PlacedBlocks>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                blocks.Created -= OnBlockCreated;
            }

            if (_inventory != null) _inventory.Changed -= OnInventoryChanged;
        }

        void OnBlockCreated(PlacedBlock block)
        {
            if (block == null) return;
            if (!block.TryGetComponent(out StorageBox box)) return;

            // 두 번 붙는 것을 막는다 — 같은 대리자를 떼고 다시 붙이면 한 번만 남는다.
            box.OpenRequested -= Open;
            box.OpenRequested += Open;
        }

        void OnInventoryChanged(Inventory inventory)
        {
            if (_window != null && _window.IsOpen) Redraw();
        }

        /// 같은 보관함에 다시 F를 누르면 닫는다(3차 프로토타입 피드백 2번).
        bool ToggledOff(StorageBox box)
        {
            if (_window == null || !_window.IsOpen || _box != box) return false;

            _window.Close();
            return true;
        }

        void Open(StorageBox box)
        {
            if (ToggledOff(box)) return;

            _box = box;
            if (_notice != null) _notice.text = string.Empty;

            Redraw();
            if (_router != null) _router.OpenPanel(_window);
        }

        void Redraw()
        {
            if (_box == null || _inventory == null) return;

            Fill(_boxSlots, _boxSlotParent, _box.Items, _inventory.Items, "보관함");
            Fill(_bagSlots, _bagSlotParent, _inventory.Items, _box.Items, "가방");

            if (_boxTitle != null)
            {
                RegionDefinition region = RegionMap.At(_box.transform.position);
                string where = region != null ? $"{region.DisplayName} " : string.Empty;
                _boxTitle.text = $"{where}보관함   {_box.Items.UsedSlots} / {_box.Items.SlotCount}칸";
            }

            if (_bagTitle != null)
            {
                _bagTitle.text = $"가방   {_inventory.UsedSlots} / {_inventory.SlotCount}칸";
            }
        }

        /// 한쪽 격자를 그린다. 누르면 to로 옮긴다 — 어느 쪽인지는 넘겨받은 짝이 결정한다.
        void Fill(List<InventorySlotView> slots, Transform parent,
                  ItemContainer from, ItemContainer to, string side)
        {
            if (parent == null || _slotTemplate == null) return;

            while (slots.Count < from.SlotCount)
            {
                InventorySlotView slot = Instantiate(_slotTemplate, parent);
                slot.gameObject.SetActive(true);
                slot.name = side + "_" + slots.Count;
                slots.Add(slot);
            }

            IReadOnlyList<ItemStack> stacks = from.Stacks;
            for (int i = 0; i < slots.Count; i++)
            {
                slots[i].gameObject.SetActive(i < from.SlotCount);
                if (i >= from.SlotCount) continue;

                if (i < stacks.Count) slots[i].Show(stacks[i], ColorFor(stacks[i].Definition));
                else slots[i].ShowEmpty();

                Bind(slots[i], from, to, i);
            }
        }

        /// 버튼은 칸마다 한 번만 손댄다. 매번 AddListener를 부르면 누를 때 호출이 쌓여
        /// 한 번 누른 것이 여러 번 옮긴다.
        void Bind(InventorySlotView slot, ItemContainer from, ItemContainer to, int index)
        {
            if (!slot.TryGetComponent(out Button button)) return;

            button.onClick.RemoveAllListeners();
            button.interactable = index < from.Stacks.Count;
            if (!button.interactable) return;

            ItemStack stack = from.Stacks[index];
            button.onClick.AddListener(() => Move(from, to, stack));
        }

        void Move(ItemContainer from, ItemContainer to, ItemStack stack)
        {
            if (stack == null || stack.Definition == null) return;

            int wanted = stack.Count;
            int moved = from.MoveTo(to, stack.Definition, wanted);

            if (moved <= 0) Say($"{stack.Definition.DisplayName}을 넣을 자리가 없습니다");
            else if (moved < wanted) Say($"{stack.Definition.DisplayName} {moved}개만 옮겼습니다 — 자리가 없습니다");
            else if (_notice != null) _notice.text = string.Empty;

            Redraw();
        }

        void Say(string text)
        {
            if (_notice != null) _notice.text = text;
        }

        /// 색은 아이템 정의가 들고 있는 고정값을 쓴다. 표시 순서와 무관해야 한다.
        static Color ColorFor(ItemDefinition definition) => definition != null ? definition.UiColor : Color.white;
    }
}
