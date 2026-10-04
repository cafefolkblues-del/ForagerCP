using UnityEngine;

namespace ForagerCP
{
    /// 아이템을 N개 들고 있어야 들어간다. 열쇠로도, 통행료로도 쓸 수 있게 소모 여부를 나눠둔다.
    [CreateAssetMenu(menuName = "ForagerCP/지역 조건/아이템", fileName = "Require_Item")]
    public class ItemRequirement : RegionRequirement
    {
        [SerializeField] ItemDefinition _item;
        [SerializeField] int _amount = 1;

        /// 켜면 들어갈 때 실제로 빠진다(통행료). 끄면 보여주기만 하면 된다(열쇠).
        [SerializeField] bool _consumeOnEnter;

        public override bool IsMet(RegionContext context)
            => _item != null && context.Inventory != null && context.Inventory.CountOf(_item) >= _amount;

        public override string Describe()
        {
            string itemName = _item != null ? _item.DisplayName : "?";
            return _consumeOnEnter ? $"{itemName} {_amount}개 필요(소모)" : $"{itemName} {_amount}개 필요";
        }

        public override bool HasCost => _consumeOnEnter;

        public override void Consume(RegionContext context)
        {
            if (!_consumeOnEnter || _item == null || context.Inventory == null) return;
            context.Inventory.Remove(_item, _amount);
        }
    }
}
