using UnityEngine;

namespace ForagerCP
{
    /// 가방 칸을 늘린다. 몇 번까지 살 수 있는지는 품목의 '한 번만'과 '선행 품목'이 정한다.
    [CreateAssetMenu(menuName = "ForagerCP/상점 효과/가방 확장", fileName = "Shop_Expand")]
    public class ExpandSlotsShopEffect : ShopEffect
    {
        [SerializeField] int _slots = 10;

        public override void Apply(ShopContext context) => context.Inventory.ExpandSlots(_slots);

        public override string Describe() => $"가방 {_slots}칸 확장";
    }
}
