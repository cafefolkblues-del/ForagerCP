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

            // 돈만 빠지고 물건이 사라지는 것을 막는다.
            // 칸 수만 보면 '같은 종류가 있지만 그 칸이 꽉 찬' 경우를 놓친다 — 인벤에게 직접 묻는다.
            if (context.Inventory.SpaceFor(_item) < _count)
            {
                reason = "가방에 넣을 자리가 없습니다";
                return false;
            }
            return true;
        }

        public override void Apply(ShopContext context) => context.Inventory.Add(_item, _count);

        public override string Describe()
            => _item != null ? $"{_item.DisplayName} {_count}개" : "아이템 미지정";
    }
}
