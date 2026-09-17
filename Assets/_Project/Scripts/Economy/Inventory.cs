using System;
using System.Collections.Generic;
using UnityEngine;

namespace ForagerCP
{
    /// 슬롯 + 스택 구조의 인벤토리. 배열이 아니라 List라 칸 수를 런타임에 늘려도 된다
    /// (확장 2-3 보관함, 2-4 용량 제약이 여기 위에 얹힌다).
    ///
    /// ⚠️ 용량 제약은 기본으로 꺼져 있다. 1장 사양이 "인벤토리 불필요"이고,
    /// 인벤 셔틀 악용 보완책이 아직 미확정이라 규칙을 코드가 먼저 정하면 안 되기 때문.
    public class Inventory : MonoBehaviour
    {
        [SerializeField] int _slotCount = 12;
        [SerializeField] bool _enforceCapacity;

        readonly List<ItemStack> _stacks = new List<ItemStack>();

        public event Action<Inventory> Changed;

        public IReadOnlyList<ItemStack> Stacks => _stacks;
        public int SlotCount => _slotCount;
        public bool EnforceCapacity => _enforceCapacity;
        public int UsedSlots => _stacks.Count;

        public int TotalCount
        {
            get
            {
                int total = 0;
                for (int i = 0; i < _stacks.Count; i++) total += _stacks[i].Count;
                return total;
            }
        }

        void Start() => Changed?.Invoke(this);

        public int CountOf(ItemDefinition definition)
        {
            if (definition == null) return 0;

            int total = 0;
            for (int i = 0; i < _stacks.Count; i++)
            {
                if (_stacks[i].Definition == definition) total += _stacks[i].Count;
            }
            return total;
        }

        /// 반환값 = 실제로 담긴 수량. amount보다 적으면 그만큼이 넘친 것이다.
        /// 넘친 분의 처리(바닥 드랍 등)는 호출하는 쪽 몫 — 규칙이 확정되면 그쪽만 고치면 된다.
        public int Add(ItemDefinition definition, int amount)
        {
            if (definition == null || amount <= 0) return 0;

            int remaining = amount;

            // 같은 종류의 기존 칸부터 채운다. 새 칸을 먼저 만들면 칸이 불필요하게 쪼개진다.
            for (int i = 0; i < _stacks.Count && remaining > 0; i++)
            {
                if (_stacks[i].Definition != definition) continue;
                remaining -= _stacks[i].Add(remaining);
            }

            while (remaining > 0)
            {
                if (_enforceCapacity && _stacks.Count >= _slotCount) break;

                var stack = new ItemStack(definition);
                remaining -= stack.Add(remaining);
                _stacks.Add(stack);
            }

            int accepted = amount - remaining;
            if (accepted > 0) Changed?.Invoke(this);
            return accepted;
        }

        /// 반환값 = 실제로 뺀 수량.
        public int Remove(ItemDefinition definition, int amount)
        {
            if (definition == null || amount <= 0) return 0;

            int remaining = amount;

            // 뒤에서부터 빼야 중간 칸이 비어 제거될 때 인덱스가 밀리지 않는다.
            for (int i = _stacks.Count - 1; i >= 0 && remaining > 0; i--)
            {
                if (_stacks[i].Definition != definition) continue;

                remaining -= _stacks[i].Remove(remaining);
                if (_stacks[i].IsEmpty) _stacks.RemoveAt(i);
            }

            int removed = amount - remaining;
            if (removed > 0) Changed?.Invoke(this);
            return removed;
        }

        /// 전량 꺼내기(변환기). 꺼낸 목록을 그대로 돌려주고 인벤은 빈다.
        public List<ItemStack> TakeAll()
        {
            var taken = new List<ItemStack>(_stacks);
            if (taken.Count == 0) return taken;

            _stacks.Clear();
            Changed?.Invoke(this);
            return taken;
        }

        public void Clear()
        {
            if (_stacks.Count == 0) return;

            _stacks.Clear();
            Changed?.Invoke(this);
        }

        /// 칸 수 확장(확장 2-3 보관함 구매 등). 줄이는 건 넘치는 칸 처리 규칙이 없어 막아둔다.
        public void ExpandSlots(int extraSlots)
        {
            if (extraSlots <= 0) return;

            _slotCount += extraSlots;
            Changed?.Invoke(this);
        }
    }
}
