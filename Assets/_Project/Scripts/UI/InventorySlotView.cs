using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ForagerCP
{
    /// 인벤토리 한 칸의 표시. 데이터는 모르고, 받은 것만 그린다.
    public class InventorySlotView : MonoBehaviour
    {
        [SerializeField] Image _icon;
        [SerializeField] TMP_Text _countText;
        [SerializeField] TMP_Text _nameText;
        [SerializeField] CanvasGroup _contentGroup;

        /// 아이콘 에셋이 아직 없을 때 쓰는 대체 색. 아트가 들어오면 아이콘이 이걸 덮는다.
        [SerializeField] Color _emptyColor = new Color(1f, 1f, 1f, 0.06f);

        public void ShowEmpty()
        {
            if (_contentGroup != null) _contentGroup.alpha = 0f;
            if (_icon != null) _icon.color = _emptyColor;
            if (_countText != null) _countText.text = string.Empty;
            if (_nameText != null) _nameText.text = string.Empty;
        }

        public void Show(ItemStack stack, Color fallbackColor)
        {
            if (stack == null || stack.Definition == null)
            {
                ShowEmpty();
                return;
            }

            if (_contentGroup != null) _contentGroup.alpha = 1f;

            if (_icon != null)
            {
                _icon.sprite = stack.Definition.Icon;
                // 아이콘이 없으면 색 칩으로라도 종류가 구분되게 한다.
                _icon.color = stack.Definition.Icon != null ? Color.white : fallbackColor;
            }

            if (_countText != null) _countText.text = stack.Count > 1 ? stack.Count.ToString() : string.Empty;
            if (_nameText != null) _nameText.text = stack.Definition.DisplayName;
        }
    }
}
