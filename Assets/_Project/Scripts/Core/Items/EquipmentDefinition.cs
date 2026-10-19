using UnityEngine;

namespace ForagerCP
{
    /// 장비가 들어가는 자리. 자리마다 하나씩만 낀다.
    public enum EquipmentSlot
    {
        /// 채집 도구. 제작으로 올린다(M2).
        Tool,

        /// 무기. 상점에서 산다(M4).
        Weapon,

        /// 방어구. 상점에서 산다(M3).
        Armor,
    }

    /// 장비 한 벌. 도구·무기·방어구가 같은 타입을 쓴다.
    ///
    /// 종류마다 클래스를 나누지 않는 이유: 셋이 다른 건 '어느 자리에 끼느냐'와 '어떤 수치를 올리느냐'
    /// 뿐이고, 둘 다 값으로 표현된다. 나누면 장착·구매·저장이 전부 세 갈래가 된다.
    ///
    /// 수치를 네 개로 못박은 것은 기획이 말한 범위가 거기까지라서다. 장비만의 예외 능력이 생기면
    /// 스킬이 한 것처럼 효과를 추상 에셋으로 빼면 된다 — 그때 가서.
    [CreateAssetMenu(menuName = "ForagerCP/장비", fileName = "Equipment")]
    public class EquipmentDefinition : ScriptableObject
    {
        [SerializeField] string _displayName;
        [SerializeField] EquipmentSlot _slot;

        /// 높을수록 상위. 구매·교체 규칙이 보는 유일한 값이다
        /// (중복 불가 / 건너뛰기 허용 / 하향 금지가 전부 이 숫자 비교로 끝난다).
        [SerializeField] int _tier = 1;

        [SerializeField] Sprite _icon;
        [SerializeField] Color _uiColor = new Color(0.78f, 0.80f, 0.85f);

        [Header("수치")]
        [SerializeField] int _harvestPower;
        [SerializeField] int _attackPower;
        /// 받은 피해를 몇 % 깎는가. 기획이 "일반·특수 피격 피해 10% 감소"로 적어
        /// 정수 차감에서 비율로 바꿨다 — 정수로 두면 적 공격력이 커질수록 방어구가 무의미해진다.
        [SerializeField] int _damageReduction;
        [SerializeField] int _maxHpBonus;

        [Header("상점")]
        /// 상점에서 파는 값. 제작으로만 얻는 것은 0으로 두고 품목에 올리지 않는다.
        [SerializeField] int _price;

        public string DisplayName => string.IsNullOrEmpty(_displayName) ? name : _displayName;
        public EquipmentSlot Slot => _slot;
        public int Tier => _tier;
        public Sprite Icon => _icon;
        public Color UiColor => _uiColor;

        public int HarvestPower => _harvestPower;
        public int AttackPower => _attackPower;
        /// 퍼센트다. 합치는 쪽(PlayerHealth)이 여러 출처를 더한 뒤 한 번만 적용한다 —
        /// 출처마다 따로 곱하면 두 개만 겹쳐도 거의 무적이 된다.
        public int DamageReduction => _damageReduction;
        public int MaxHpBonus => _maxHpBonus;
        public int Price => Mathf.Max(0, _price);

        public string Describe()
        {
            var parts = new System.Collections.Generic.List<string>();
            if (_harvestPower != 0) parts.Add($"채집 +{_harvestPower}");
            if (_attackPower != 0) parts.Add($"공격 +{_attackPower}");
            if (_damageReduction != 0) parts.Add($"피해 감소 {_damageReduction}");
            if (_maxHpBonus != 0) parts.Add($"최대 체력 +{_maxHpBonus}");

            return parts.Count == 0 ? "효과 없음" : string.Join(" · ", parts);
        }
    }
}
