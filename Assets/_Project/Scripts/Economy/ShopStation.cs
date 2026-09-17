using System;
using UnityEngine;

namespace ForagerCP
{
    /// 아이템 구매(사양 1-8). MVP는 상점 UI 없이 F 한 번에 구매 가능한 첫 품목을 산다.
    public class ShopStation : MonoBehaviour, IInteractable
    {
        [SerializeField] GoldWallet _gold;
        [SerializeField] HarvestPower _harvestPower;
        [SerializeField] ShopItem[] _items = new ShopItem[1];

        public event Action<ShopItem> Purchased;

        public Transform Transform => transform;
        public string Label => "상점";

        /// ConverterStation과 같은 이유의 방어 탐색(프리팹 배치 시 씬 참조가 비어 옴).
        void Awake()
        {
            if (_gold == null) _gold = FindFirstObjectByType<GoldWallet>();
            if (_harvestPower == null) _harvestPower = FindFirstObjectByType<HarvestPower>();
            if (_gold == null || _harvestPower == null) Debug.LogError($"{name}: 상점 참조 미배선", this);
        }

        public void Interact()
        {
            ShopItem item = FindPurchasableItem();
            if (item == null) return; // 예외 1-10: 1회성 아이템 재구매 / 살 게 없으면 무동작

            // TrySpend가 잔액 확인과 차감을 함께 처리 → 실패 시 차감도 효과 적용도 일어나지 않는다.
            if (!_gold.TrySpend(item.Price)) return;

            item.MarkPurchased();
            _harvestPower.AddBonus(item.HarvestPowerBonus);
            Purchased?.Invoke(item);
        }

        ShopItem FindPurchasableItem()
        {
            for (int i = 0; i < _items.Length; i++)
            {
                if (_items[i] != null && _items[i].CanPurchase) return _items[i];
            }
            return null;
        }
    }
}
