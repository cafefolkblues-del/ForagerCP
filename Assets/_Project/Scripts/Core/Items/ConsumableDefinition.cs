using System.Collections.Generic;
using UnityEngine;

namespace ForagerCP
{
    /// 쓰면 사라지는 아이템. 회복약·버프약이 이쪽.
    ///
    /// ItemDefinition에 효과를 달지 않고 상속으로 가른 이유: 광물까지 '쓸 수 있는 것'이 되면
    /// 핫바에서 E를 눌렀을 때 무엇이 일어날지 아이템마다 따져야 한다.
    /// 타입으로 갈라두면 쓰는 쪽이 '소모품이냐'만 물으면 된다.
    [CreateAssetMenu(menuName = "ForagerCP/소모품", fileName = "Consumable")]
    public class ConsumableDefinition : ItemDefinition
    {
        [Header("쓸 때")]
        [SerializeField] List<ConsumableEffect> _effects = new List<ConsumableEffect>();

        /// 한 번에 몇 개가 닳는지. 보통 1.
        [SerializeField] int _costPerUse = 1;

        public IReadOnlyList<ConsumableEffect> Effects => _effects;
        public int CostPerUse => Mathf.Max(1, _costPerUse);

        public string DescribeUse()
        {
            var parts = new List<string>();
            for (int i = 0; i < _effects.Count; i++)
            {
                if (_effects[i] != null) parts.Add(_effects[i].Describe());
            }
            return parts.Count == 0 ? "효과 없음" : string.Join(" · ", parts);
        }

        /// 하나라도 지금 쓸 이유가 있으면 쓴다. 체력이 꽉 찼는데 회복약이 닳는 일을 막는다.
        public bool CanUse(ConsumableContext context, out string reason)
        {
            reason = "지금은 쓸 수 없습니다";

            for (int i = 0; i < _effects.Count; i++)
            {
                if (_effects[i] == null) continue;
                if (_effects[i].CanUse(context, out string why)) return true;

                reason = why;
            }
            return false;
        }

        public void Use(ConsumableContext context)
        {
            for (int i = 0; i < _effects.Count; i++)
            {
                if (_effects[i] != null) _effects[i].Use(context);
            }
        }
    }
}
