using System;
using System.Collections.Generic;
using UnityEngine;

namespace ForagerCP
{
    /// 광물 → 골드 변환기(사양 1-7). 보유 광물 전량 일괄 변환.
    /// 광물 종류가 늘어난 뒤로는 개수 × 고정가가 아니라 종류별 GoldValue를 합산한다.
    public class ConverterStation : MonoBehaviour, IInteractable
    {
        [SerializeField] Inventory _inventory;
        [SerializeField] GoldWallet _gold;

        /// 종류별 가격에 일괄로 곱하는 배율. 경제 전체를 한 손잡이로 돌리려고 남겨둔 값.
        [SerializeField] float _priceMultiplier = 1f;

        public event Action<int, int> Converted; // (넘긴 개수, 받은 골드)

        public Transform Transform => transform;
        public string Label => "광물 변환기";

        /// 팔레트로 찍어 넣은 프리팹은 씬 참조 슬롯이 비어 온다.
        /// 배선을 빠뜨렸을 때 조용히 무동작이 되지 않도록 방어 탐색만 하고, 없으면 크게 알린다.
        void Awake()
        {
            if (_inventory == null) _inventory = FindFirstObjectByType<Inventory>();
            if (_gold == null) _gold = FindFirstObjectByType<GoldWallet>();
            if (_inventory == null || _gold == null) Debug.LogError($"{name}: 변환기 참조 미배선", this);
        }

        public void Interact()
        {
            if (_inventory.TotalCount <= 0) return; // 예외 1-10: 보유 0개면 무발생

            // 먼저 꺼내고(차감) 그 다음 지급 — 사양 1-7의 고정 순서.
            List<ItemStack> taken = _inventory.TakeAll();

            int count = 0;
            int reward = 0;
            for (int i = 0; i < taken.Count; i++)
            {
                ItemStack stack = taken[i];
                if (stack.Definition == null) continue;

                count += stack.Count;
                reward += Mathf.RoundToInt(stack.Count * stack.Definition.GoldValue * _priceMultiplier);
            }

            _gold.Add(reward);
            Converted?.Invoke(count, reward);
        }
    }
}
