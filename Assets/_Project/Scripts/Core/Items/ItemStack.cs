using System;
using UnityEngine;

namespace ForagerCP
{
    /// 인벤토리 한 칸. struct가 아니라 class인 이유:
    /// List<ItemStack> 안에서 수량만 바꿔 쓰는데, struct면 복사본이 수정돼 원본이 안 바뀐다.
    [Serializable]
    public class ItemStack
    {
        [SerializeField] ItemDefinition _definition;
        [SerializeField] int _count;

        public ItemStack(ItemDefinition definition, int count = 0)
        {
            _definition = definition;
            _count = Mathf.Max(0, count);
        }

        public ItemDefinition Definition => _definition;
        public int Count => _count;
        public bool IsEmpty => _count <= 0;

        /// 이 칸에 더 들어갈 수 있는 수량.
        public int Space => _definition == null ? 0 : Mathf.Max(0, _definition.MaxStack - _count);

        /// 반환값 = 실제로 담긴 수량. 넘친 만큼은 호출한 쪽이 처리한다(확장 2-4 바닥 드랍 자리).
        public int Add(int amount)
        {
            if (amount <= 0) return 0;

            int accepted = Mathf.Min(amount, Space);
            _count += accepted;
            return accepted;
        }

        /// 반환값 = 실제로 뺀 수량.
        public int Remove(int amount)
        {
            if (amount <= 0) return 0;

            int removed = Mathf.Min(amount, _count);
            _count -= removed;
            return removed;
        }
    }
}
