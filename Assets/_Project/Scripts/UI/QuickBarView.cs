using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ForagerCP
{
    /// 화면 하단 핫바 표시. 칸은 템플릿을 복제해 만들고, QuickBar가 바뀔 때만 다시 그린다.
    /// 선택 칸은 테두리를 켜서 알린다(아이콘이 없어도 어디를 골랐는지 보이게).
    public class QuickBarView : MonoBehaviour
    {
        [SerializeField] QuickBar _quickBar;
        [SerializeField] Transform _slotParent;
        [SerializeField] RectTransform _slotTemplate;
        [SerializeField] TMP_Text _selectedLabel;

        [SerializeField] Color _emptyIconColor = new Color(1f, 1f, 1f, 0.07f);
        [SerializeField] Vector2 _selectedSize = new Vector2(76f, 76f);
        [SerializeField] Vector2 _normalSize = new Vector2(64f, 64f);

        readonly List<RectTransform> _slots = new List<RectTransform>();
        readonly List<Image> _icons = new List<Image>();
        readonly List<Image> _borders = new List<Image>();
        readonly List<TMP_Text> _counts = new List<TMP_Text>();

        void Awake()
        {
            if (_quickBar == null) _quickBar = FindFirstObjectByType<QuickBar>();
            if (_slotTemplate != null) _slotTemplate.gameObject.SetActive(false);
        }

        void OnEnable()
        {
            if (_quickBar == null) return;

            _quickBar.Changed += Redraw;
            Redraw(_quickBar);
        }

        void OnDisable()
        {
            if (_quickBar == null) return;
            _quickBar.Changed -= Redraw;
        }

        void Redraw(QuickBar quickBar)
        {
            EnsureSlots(quickBar.SlotCount);

            for (int i = 0; i < _slots.Count; i++)
            {
                bool used = i < quickBar.SlotCount;
                _slots[i].gameObject.SetActive(used);
                if (!used) continue;

                bool selected = i == quickBar.SelectedIndex;
                _borders[i].enabled = selected;

                // 고른 칸을 살짝 키워 테두리만으로 못 알아보는 경우를 막는다.
                _slots[i].sizeDelta = selected ? _selectedSize : _normalSize;

                ItemStack stack = quickBar.StackAt(i);
                if (stack == null || stack.Definition == null)
                {
                    _icons[i].sprite = null;
                    _icons[i].color = _emptyIconColor;
                    _counts[i].text = string.Empty;
                    continue;
                }

                _icons[i].sprite = stack.Definition.Icon;
                _icons[i].color = stack.Definition.Icon != null ? Color.white : stack.Definition.UiColor;
                _counts[i].text = stack.Count > 1 ? stack.Count.ToString() : string.Empty;
            }

            if (_selectedLabel == null) return;

            ItemDefinition selectedItem = quickBar.SelectedItem;
            _selectedLabel.text = selectedItem == null
                ? string.Empty
                : $"{selectedItem.DisplayName}  {quickBar.SelectedCount}";
        }

        void EnsureSlots(int wanted)
        {
            if (_slotTemplate == null || _slotParent == null) return;

            while (_slots.Count < wanted)
            {
                RectTransform slot = Instantiate(_slotTemplate, _slotParent);
                slot.gameObject.SetActive(true);
                slot.name = "Slot_" + _slots.Count;

                _slots.Add(slot);
                _icons.Add(slot.Find("Icon").GetComponent<Image>());
                _borders.Add(slot.Find("Border").GetComponent<Image>());
                _counts.Add(slot.Find("Count").GetComponent<TMP_Text>());
            }
        }
    }
}
