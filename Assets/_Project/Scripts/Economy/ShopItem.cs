using System;
using UnityEngine;

namespace ForagerCP
{
    /// 상점 품목 데이터. MVP는 1종뿐이라 ScriptableObject 대신 인스펙터 직렬화 클래스로 둔다.
    /// 품목이 늘어 에셋 단위 관리가 필요해지면 그대로 SO로 승격 가능한 순수 데이터 형태.
    [Serializable]
    public class ShopItem
    {
        [SerializeField] string _displayName = "채집 강화";
        [SerializeField] int _price = 50;
        [SerializeField] int _harvestPowerBonus = 5;
        [SerializeField] bool _oneTimePurchase = true;

        /// 검 티어를 파는 품목이면 여기에 꽂는다. 비어 있으면 채집력 강화 같은 일반 품목.
        [SerializeField] WeaponTier _weaponTier;

        public string DisplayName => _displayName;
        public int Price => _price;
        public int HarvestPowerBonus => _harvestPowerBonus;
        public bool OneTimePurchase => _oneTimePurchase;
        public WeaponTier WeaponTier => _weaponTier;

        /// 세이브 미구현(재실행 시 초기화 허용)이라 런타임 플래그로만 들고 있는다.
        public bool Purchased { get; private set; }

        public bool CanPurchase => !(_oneTimePurchase && Purchased);

        public void MarkPurchased() => Purchased = true;
    }
}
