using System.Collections.Generic;
using UnityEngine;

namespace ForagerCP
{
    /// 일정 시간 동안 걸리는 강화 하나.
    ///
    /// 수치를 장비와 같은 종류로 맞춘 이유: 합산하는 자리가 늘어나면 안 된다.
    /// 공격력을 쓰는 쪽이 '장비 + 버프'만 더하면 되게 둔다.
    ///
    /// ⚠️ 전부 **퍼센트**다. 기획이 "기본 능력치에 각각의 증가율을 더한 뒤 한 번 계산"으로
    /// 못박았으므로, 더하는 것은 비율이고 곱하는 것은 마지막 한 번뿐이다.
    [CreateAssetMenu(menuName = "ForagerCP/버프", fileName = "Buff")]
    public class BuffDefinition : ScriptableObject
    {
        [SerializeField] string _displayName;
        [SerializeField] Color _uiColor = new Color(0.72f, 0.55f, 0.95f);
        [SerializeField] float _seconds = 60f;

        [Header("수치 (전부 %)")]
        /// 광물 공격 간격을 몇 % 줄이는가. 채집 버프는 위력이 아니라 **속도**다.
        [SerializeField] int _harvestSpeed;

        /// 평타 피해를 몇 % 늘리는가.
        [SerializeField] int _attackPower;

        /// 받은 피해를 몇 % 줄이는가.
        [SerializeField] int _damageReduction;

        public string DisplayName => string.IsNullOrEmpty(_displayName) ? name : _displayName;
        public Color UiColor => _uiColor;
        public float Seconds => Mathf.Max(1f, _seconds);

        public int HarvestSpeed => _harvestSpeed;
        public int AttackPower => _attackPower;
        public int DamageReduction => _damageReduction;

        public string Describe()
        {
            var parts = new List<string>();
            if (_harvestSpeed != 0) parts.Add($"채집 속도 +{_harvestSpeed}%");
            if (_attackPower != 0) parts.Add($"공격 +{_attackPower}%");
            if (_damageReduction != 0) parts.Add($"피해 감소 {_damageReduction}%");

            string what = parts.Count == 0 ? "효과 없음" : string.Join(" · ", parts);
            return $"{what} ({_seconds:0}초)";
        }
    }
}
