using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace ForagerCP
{
    /// 인벤토리 창. 칸을 템플릿에서 복제해 만들고, Inventory가 바뀔 때만 다시 그린다.
    /// 칸 수는 Inventory.SlotCount를 따라가므로 보관함 확장(2-3)이 들어와도 여기는 안 고쳐도 된다.
    public class InventoryPanel : MonoBehaviour
    {
        [SerializeField] Inventory _inventory;
        [SerializeField] Transform _slotParent;
        [SerializeField] InventorySlotView _slotTemplate;
        [SerializeField] TMP_Text _summaryText;

        readonly List<InventorySlotView> _slots = new List<InventorySlotView>();

        void Awake()
        {
            if (_inventory == null) _inventory = FindFirstObjectByType<Inventory>();
            if (_slotTemplate != null) _slotTemplate.gameObject.SetActive(false);
        }

        void OnEnable()
        {
            if (_inventory == null) return;

            _inventory.Changed += Redraw;
            Redraw(_inventory);
        }

        void OnDisable()
        {
            if (_inventory == null) return;
            _inventory.Changed -= Redraw;
        }

        void Redraw(Inventory inventory)
        {
            EnsureSlotCount(inventory.SlotCount);

            IReadOnlyList<ItemStack> stacks = inventory.Stacks;
            for (int i = 0; i < _slots.Count; i++)
            {
                if (i < stacks.Count) _slots[i].Show(stacks[i], ColorFor(stacks[i].Definition));
                else _slots[i].ShowEmpty();
            }

            if (_summaryText == null) return;
            _summaryText.text = inventory.EnforceCapacity
                ? $"{inventory.UsedSlots} / {inventory.SlotCount}칸   ·   총 {inventory.TotalCount}개"
                : $"{inventory.UsedSlots}칸 사용   ·   총 {inventory.TotalCount}개";
        }

        void EnsureSlotCount(int wanted)
        {
            if (_slotTemplate == null || _slotParent == null) return;

            // 칸은 한 번 만들면 재사용한다. 열 때마다 만들고 부수면 GC가 튄다.
            while (_slots.Count < wanted)
            {
                InventorySlotView slot = Instantiate(_slotTemplate, _slotParent);
                slot.gameObject.SetActive(true);
                slot.name = "Slot_" + _slots.Count;
                _slots.Add(slot);
            }

            for (int i = 0; i < _slots.Count; i++) _slots[i].gameObject.SetActive(i < wanted);
        }

        /// 색은 아이템 정의가 들고 있는 고정값을 쓴다. 표시 순서와 무관해야 한다.
        static Color ColorFor(ItemDefinition definition) => definition != null ? definition.UiColor : Color.white;
    }
}
