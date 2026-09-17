using System.Text;
using TMPro;
using UnityEngine;

namespace ForagerCP
{
    /// 사양 1-9. 데이터의 Changed 이벤트만 구독해서 값이 바뀐 직후 갱신한다(폴링 없음).
    /// 광물이 여러 종류가 된 뒤로는 종류별 수량을 한 줄로 이어 붙여 보여준다
    /// (제대로 된 인벤 화면이 붙기 전까지 쓰는 임시 표시).
    public class HudView : MonoBehaviour
    {
        [SerializeField] PlayerLevel _level;
        [SerializeField] Inventory _inventory;
        [SerializeField] GoldWallet _gold;

        [SerializeField] TMP_Text _levelText;
        [SerializeField] TMP_Text _expText;
        [SerializeField] TMP_Text _mineralText;
        [SerializeField] TMP_Text _goldText;

        readonly StringBuilder _builder = new StringBuilder();

        void OnEnable()
        {
            _level.Changed += OnLevelChanged;
            _inventory.Changed += OnInventoryChanged;
            _gold.Changed += OnGoldChanged;

            // 이벤트 구독 전에 이미 값이 들어간 경우를 대비해 한 번 강제 동기화.
            OnLevelChanged(_level);
            OnInventoryChanged(_inventory);
            OnGoldChanged(_gold.Gold);
        }

        void OnDisable()
        {
            _level.Changed -= OnLevelChanged;
            _inventory.Changed -= OnInventoryChanged;
            _gold.Changed -= OnGoldChanged;
        }

        void OnLevelChanged(PlayerLevel level)
        {
            if (_levelText != null) _levelText.text = $"Lv. {level.Level}";
            if (_expText == null) return;

            _expText.text = level.IsMaxLevel
                ? $"EXP {level.Exp} / -  (MAX)"
                : $"EXP {level.Exp} / {level.ExpToNext}";
        }

        void OnInventoryChanged(Inventory inventory)
        {
            if (_mineralText == null) return;

            if (inventory.UsedSlots == 0)
            {
                _mineralText.text = "광물 없음";
                return;
            }

            // StringBuilder: 채집할 때마다 갱신이라 문자열 이어붙이기가 매번 쓰레기를 만든다.
            _builder.Clear();
            for (int i = 0; i < inventory.Stacks.Count; i++)
            {
                ItemStack stack = inventory.Stacks[i];
                if (stack.Definition == null) continue;

                if (_builder.Length > 0) _builder.Append("  ");
                _builder.Append(stack.Definition.DisplayName).Append(' ').Append(stack.Count);
            }

            if (inventory.EnforceCapacity)
                _builder.Append("   (").Append(inventory.UsedSlots).Append('/').Append(inventory.SlotCount).Append("칸)");

            _mineralText.text = _builder.ToString();
        }

        void OnGoldChanged(int gold)
        {
            if (_goldText != null) _goldText.text = $"골드 {gold}";
        }
    }
}
