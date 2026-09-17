using System;
using UnityEngine;

namespace ForagerCP
{
    public class GoldWallet : MonoBehaviour
    {
        [SerializeField] int _startGold = 0;

        public event Action<int> Changed;

        public int Gold { get; private set; }

        void Awake() => Gold = _startGold;

        void Start() => Changed?.Invoke(Gold);

        public void Add(int amount)
        {
            if (amount <= 0) return;
            Gold += amount;
            Changed?.Invoke(Gold);
        }

        /// bool 반환: 구매 쪽에서 "차감 성공했을 때만 효과 적용"을 한 줄로 보장하려고
        /// 잔액 확인과 차감을 한 곳에 묶는다(사양 1-8 / 1-10).
        public bool TrySpend(int amount)
        {
            if (amount <= 0) return false;
            if (Gold < amount) return false;
            Gold -= amount;
            Changed?.Invoke(Gold);
            return true;
        }
    }
}
