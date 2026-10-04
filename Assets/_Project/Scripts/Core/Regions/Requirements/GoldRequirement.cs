using UnityEngine;

namespace ForagerCP
{
    /// 골드를 내고 들어간다. 원작 포저의 '땅 구매'가 이 모양이라 올 가능성이 높아 미리 깔아둔다.
    [CreateAssetMenu(menuName = "ForagerCP/지역 조건/골드", fileName = "Require_Gold")]
    public class GoldRequirement : RegionRequirement
    {
        [SerializeField] int _price = 100;

        /// 끄면 '그만큼 들고 있기만 하면' 통과한다(자격 증명). 켜면 실제로 빠진다(구매).
        [SerializeField] bool _spendOnEnter = true;

        public override bool IsMet(RegionContext context)
            => context.Gold != null && context.Gold.Gold >= _price;

        public override string Describe() => _spendOnEnter ? $"{_price} 골드로 개방" : $"{_price} 골드 보유 필요";

        public override bool HasCost => _spendOnEnter;

        public override void Consume(RegionContext context)
        {
            if (!_spendOnEnter || context.Gold == null) return;
            context.Gold.TrySpend(_price);
        }
    }
}
