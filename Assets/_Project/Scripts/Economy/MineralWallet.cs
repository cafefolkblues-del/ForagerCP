using System;
using UnityEngine;

namespace ForagerCP
{
    /// 광물 1종이라 인벤토리 없이 개수만 든다(사양 1-6).
    /// 확장기능 2-4(용량 제약)가 붙을 자리는 Add의 반환값으로 열어둔다.
    public class MineralWallet : MonoBehaviour
    {
        [SerializeField] int _startCount = 0;

        public event Action<int> Changed;

        public int Count { get; private set; }

        void Awake() => Count = _startCount;

        void Start() => Changed?.Invoke(Count);

        /// 반환값 = 실제로 담긴 개수. 지금은 항상 amount지만,
        /// 인벤 용량 제약이 들어오면 여기서 잘라내고 나머지를 바닥 드랍으로 넘긴다.
        public int Add(int amount)
        {
            if (amount <= 0) return 0;
            Count += amount;
            Changed?.Invoke(Count);
            return amount;
        }

        public int ConsumeAll()
        {
            int consumed = Count;
            if (consumed <= 0) return 0;
            Count = 0;
            Changed?.Invoke(Count);
            return consumed;
        }
    }
}
