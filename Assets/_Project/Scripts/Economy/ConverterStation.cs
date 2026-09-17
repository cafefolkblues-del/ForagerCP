using System;
using UnityEngine;

namespace ForagerCP
{
    /// 광물 → 골드 변환기(사양 1-7). 보유 광물 전량 일괄 변환.
    public class ConverterStation : MonoBehaviour, IInteractable
    {
        [SerializeField] MineralWallet _minerals;
        [SerializeField] GoldWallet _gold;
        [SerializeField] int _goldPerMineral = 10;

        public event Action<int, int> Converted; // (광물 수, 받은 골드)

        public Transform Transform => transform;
        public string Label => "광물 변환기";

        public void Interact()
        {
            int count = _minerals.Count;
            if (count <= 0) return; // 예외 1-10: 보유 0개면 무발생

            int reward = count * _goldPerMineral;
            _minerals.ConsumeAll(); // 차감 먼저, 그 다음 지급 (사양 1-7의 고정 순서)
            _gold.Add(reward);

            Converted?.Invoke(count, reward);
        }
    }
}
