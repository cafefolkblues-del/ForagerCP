using UnityEngine;

namespace ForagerCP
{
    /// 장비를 지급한다. 사는 순간 끼워진다 — 되팔기도 보관도 안 되므로 들고 다닐 곳이 없다.
    [CreateAssetMenu(menuName = "ForagerCP/상점 효과/장비", fileName = "Shop_Equip")]
    public class EquipShopEffect : ShopEffect
    {
        [SerializeField] EquipmentDefinition _equipment;

        /// 중복 불가 / 등급 건너뛰기 허용 / 하향 금지 판정은 장비 쪽에 이미 있다.
        /// 여기서 또 규칙을 쓰면 두 곳이 어긋난다.
        public override bool CanBuy(ShopContext context, out string reason)
        {
            reason = null;
            if (_equipment == null) { reason = "장비가 비어 있습니다"; return false; }

            return context.Equipment.CanEquip(_equipment, out reason);
        }

        public override void Apply(ShopContext context) => context.Equipment.Equip(_equipment);

        public override string Describe()
            => _equipment != null ? $"{_equipment.DisplayName} · {_equipment.Describe()}" : "장비 미지정";
    }
}
