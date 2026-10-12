using System;
using System.Collections.Generic;
using UnityEngine;

namespace ForagerCP
{
    /// 광물 → 골드 변환기. 종류와 수량을 골라 판다.
    ///
    /// 전량 일괄에서 바뀐 이유: 팔 수 없는 광물(특수·엔딩)이 생겼고, 아껴둘 재료와 팔 재료가
    /// 갈리기 시작했다. 한 번에 다 털어버리면 제작 재료까지 날아간다.
    public class ConverterStation : MonoBehaviour, IInteractable, ISaveMilestone
    {
        [SerializeField] Inventory _inventory;
        [SerializeField] GoldWallet _gold;

        /// 종류별 가격에 일괄로 곱하는 배율. 경제 전체를 한 손잡이로 돌리려고 남겨둔 값.
        [SerializeField] float _priceMultiplier = 1f;

        public event Action<int, int> Converted; // (넘긴 개수, 받은 골드)

        /// 판 자원은 되돌아오지 않는다. 거래와 같은 취급.
        public event Action<string> Milestone;

        /// F를 눌렀다. 화면이 이걸 듣고 자기를 연다.
        public event Action<ConverterStation> OpenRequested;

        public Transform Transform => transform;
        public string Label => "광물 변환기 열기";

        void Awake()
        {
            if (_inventory == null) _inventory = FindFirstObjectByType<Inventory>();
            if (_gold == null) _gold = FindFirstObjectByType<GoldWallet>();
            if (_inventory == null || _gold == null) Debug.LogError($"{name}: 변환기 참조 미배선", this);
        }

        /// 개당 받는 골드. 화면이 '예상 골드'를 뽑을 때도 이걸 쓴다 —
        /// 보여준 값과 실제 받는 값이 어긋나지 않게 계산을 한 곳에 둔다.
        public int UnitPrice(ItemDefinition item)
            => item == null ? 0 : Mathf.RoundToInt(item.GoldValue * _priceMultiplier);

        public int PriceFor(ItemDefinition item, int count) => UnitPrice(item) * Mathf.Max(0, count);

        /// 팔 수 있는 것만 모은다. 화면이 줄을 그릴 때 쓴다.
        public void CollectSellable(List<ItemStack> into)
        {
            into.Clear();
            if (_inventory == null) return;

            IReadOnlyList<ItemStack> stacks = _inventory.Stacks;
            for (int i = 0; i < stacks.Count; i++)
            {
                ItemStack stack = stacks[i];
                if (stack.Definition == null || !stack.Definition.Sellable) continue;

                into.Add(stack);
            }
        }

        public bool Sell(ItemDefinition item, int count)
        {
            if (item == null || count <= 0) return false;
            if (!item.Sellable) return false;

            // 먼저 빼고 그 다음 지급 — 실제로 빠진 만큼만 값을 쳐준다.
            int removed = _inventory.Remove(item, count);
            if (removed <= 0) return false;

            int reward = PriceFor(item, removed);
            _gold.Add(reward);

            Converted?.Invoke(removed, reward);
            Milestone?.Invoke($"판매: {item.DisplayName} {removed}개 {reward}골드");
            return true;
        }

        public void Interact() => OpenRequested?.Invoke(this);
    }
}
