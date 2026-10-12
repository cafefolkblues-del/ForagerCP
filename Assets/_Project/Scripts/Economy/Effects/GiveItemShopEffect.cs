using UnityEngine;

namespace ForagerCP
{
    /// 아이템을 인벤토리에 넣는다. 회복·버프 소모품이 이쪽.
    [CreateAssetMenu(menuName = "ForagerCP/상점 효과/아이템", fileName = "Shop_Item")]
    public class GiveItemShopEffect : ShopEffect
    {
        [SerializeField] ItemDefinition _item;
        [SerializeField] int _count = 1;

        public override bool CanBuy(ShopContext context, out string reason)
        {
            reason = null;
            if (_item == null) { reason = "아이템이 비어 있습니다"; return false; }

            // 칸이 꽉 차 못 넣을 수 있다. 돈만 빠지고 물건이 사라지는 것을 막는다.
            if (context.Inventory.EnforceCapacity
                && context.Inventory.UsedSlots >= context.Inventory.SlotCount
                && context.Inventory.CountOf(_item) == 0)
            {
                reason = "가방이 가득 찼습니다";
                return false;
            }
            return true;
        }

        public override void Apply(ShopContext context) => context.Inventory.Add(_item, _count);

        public override string Describe()
            => _item != null ? $"{_item.DisplayName} {_count}개" : "아이템 미지정";
    }
}
