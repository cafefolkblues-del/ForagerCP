using UnityEngine;

namespace ForagerCP
{
    /// 상점 효과가 건드릴 수 있는 것들. RegionContext·SkillContext와 같은 자리.
    public class ShopContext
    {
        public Inventory Inventory;
        public PlayerEquipment Equipment;
        public GoldWallet Gold;
        public HarvestPower Harvest;
    }

    /// 상점 품목 하나가 실제로 주는 것.
    ///
    /// 품목 종류마다 필드를 늘리지 않는 이유: 회복·버프·무기·방어구·이동편의·인벤확장이
    /// 전부 다른 일을 하는데, 한 클래스에 필드로 쌓으면 품목 하나가 쓰지도 않는 값을 열 개씩 들게 된다.
    /// 효과를 에셋으로 빼면 품목은 효과 목록만 들고, 새 종류는 상속 하나로 끝난다.
    public abstract class ShopEffect : ScriptableObject
    {
        /// 살 수 있는지. 못 사면 reason에 사람이 읽을 이유를 담는다.
        public virtual bool CanBuy(ShopContext context, out string reason)
        {
            reason = null;
            return true;
        }

        public abstract void Apply(ShopContext context);

        /// 화면에 그대로 띄울 설명. 효과가 자기를 설명해야 UI에 종류별 분기가 쌓이지 않는다.
        public abstract string Describe();
    }
}
