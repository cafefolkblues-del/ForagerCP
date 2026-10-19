using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ForagerCP
{
    /// 인벤 탭(책 화면). 왼쪽 쪽은 칸 격자, 오른쪽 쪽은 고른 아이템의 그림·이름·설명.
    ///
    /// 칸을 템플릿에서 복제해 만들고, Inventory가 바뀔 때만 다시 그린다.
    /// 칸 수는 Inventory.SlotCount를 따라가므로 가방이 늘어도 여기는 안 고쳐도 된다 —
    /// 한 쪽에 다 안 들어가는 만큼은 격자가 스크롤된다.
    public class InventoryPanel : MonoBehaviour
    {
        [SerializeField] Inventory _inventory;
        [SerializeField] Transform _slotParent;
        [SerializeField] InventorySlotView _slotTemplate;

        [Header("오른쪽 — 고른 아이템")]
        [SerializeField] GameObject _detailRoot;
        [SerializeField] Image _detailIcon;
        [SerializeField] TMP_Text _detailName;
        [SerializeField] TMP_Text _detailDescription;

        readonly List<InventorySlotView> _slots = new List<InventorySlotView>();

        /// 칸 번호가 아니라 아이템으로 기억한다. 칸 순서가 바뀌어도(정렬·소모) 고른 것이 따라간다.
        ItemDefinition _selected;

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

            // 고른 것을 다 써버렸으면 선택을 푼다. 빈 칸 설명이 남아 있으면 아직 가진 줄 안다.
            if (_selected != null && inventory.CountOf(_selected) <= 0) _selected = null;

            for (int i = 0; i < _slots.Count; i++)
            {
                ItemStack stack = i < stacks.Count ? stacks[i] : null;

                if (stack != null) _slots[i].Show(stack, ColorFor(stack.Definition));
                else _slots[i].ShowEmpty();

                _slots[i].SetSelected(stack != null && stack.Definition == _selected);
            }

            RedrawDetail();
        }

        void Select(int index)
        {
            IReadOnlyList<ItemStack> stacks = _inventory.Stacks;
            _selected = index < stacks.Count ? stacks[index].Definition : null;
            Redraw(_inventory);
        }

        void RedrawDetail()
        {
            if (_detailRoot != null) _detailRoot.SetActive(_selected != null);
            if (_selected == null) return;

            if (_detailIcon != null)
            {
                _detailIcon.sprite = _selected.Icon;
                _detailIcon.color = _selected.Icon != null ? Color.white : _selected.UiColor;
            }
            if (_detailName != null) _detailName.text = _selected.DisplayName;
            if (_detailDescription != null) _detailDescription.text = _selected.Description;
        }

        void EnsureSlotCount(int wanted)
        {
            if (_slotTemplate == null || _slotParent == null) return;

            // 칸은 한 번 만들면 재사용한다. 열 때마다 만들고 부수면 GC가 튄다.
            // 버튼도 만들 때 한 번만 잇는다 — 다시 그릴 때마다 붙이면 누를 때 호출이 쌓인다.
            while (_slots.Count < wanted)
            {
                InventorySlotView slot = Instantiate(_slotTemplate, _slotParent);
                slot.gameObject.SetActive(true);
                slot.name = "Slot_" + _slots.Count;

                int index = _slots.Count;
                if (slot.TryGetComponent(out Button button)) button.onClick.AddListener(() => Select(index));

                _slots.Add(slot);
            }

            for (int i = 0; i < _slots.Count; i++) _slots[i].gameObject.SetActive(i < wanted);
        }

        /// 색은 아이템 정의가 들고 있는 고정값을 쓴다. 표시 순서와 무관해야 한다.
        static Color ColorFor(ItemDefinition definition) => definition != null ? definition.UiColor : Color.white;
    }
}
