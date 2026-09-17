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

        /// 팔레트로 찍어 넣은 프리팹은 씬 참조 슬롯이 비어 온다.
        /// 배선을 빠뜨렸을 때 조용히 무동작이 되지 않도록 방어 탐색만 하고, 없으면 크게 알린다.
        void Awake()
        {
            if (_minerals == null) _minerals = FindFirstObjectByType<MineralWallet>();
            if (_gold == null) _gold = FindFirstObjectByType<GoldWallet>();
            if (_minerals == null || _gold == null) Debug.LogError($"{name}: 변환기 참조 미배선", this);
        }

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
