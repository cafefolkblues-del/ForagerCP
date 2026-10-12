using System;
using System.Collections.Generic;
using UnityEngine;

namespace ForagerCP
{
    /// 슬롯 + 스택 구조의 인벤토리. 배열이 아니라 List라 칸 수를 런타임에 늘려도 된다
    /// (확장 2-3 보관함, 2-4 용량 제약이 여기 위에 얹힌다).
    ///
    /// 용량 제한이 켜져 있다(기본 20칸, 상점에서 10칸씩 두 번 확장).
    /// 넘치는 분은 버리지 않는다 — 돌려주는 자리는 DropService.GiveOrDrop으로 바닥에 떨구고,
    /// 사는 자리는 SpaceFor로 미리 막는다. 캔 자원은 애초에 바닥에 떨어지므로
    /// 가방이 꽉 차 못 주우면 그대로 두었다가 수명이 다해 사라진다(확정).
    public class Inventory : MonoBehaviour, ISaveable
    {
        [SerializeField] int _slotCount = 20;
        [SerializeField] bool _enforceCapacity = true;
        [SerializeField] SaveCatalog _catalog;

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

        /// 지금 이 종류를 몇 개까지 받을 수 있는지.
        ///
        /// Add를 불러보고 반환값으로 아는 방법은 쓸 수 없는 자리가 있다 — 사는 쪽은
        /// 돈을 내기 전에 알아야 하므로. 기존 칸의 남은 자리와 새로 만들 수 있는 칸을 함께 센다.
        public int SpaceFor(ItemDefinition definition)
        {
            if (definition == null) return 0;
            if (!_enforceCapacity) return int.MaxValue;

            int space = 0;
            for (int i = 0; i < _stacks.Count; i++)
            {
                if (_stacks[i].Definition == definition) space += _stacks[i].Space;
            }

            int freeSlots = Mathf.Max(0, _slotCount - _stacks.Count);
            return space + freeSlots * definition.MaxStack;
        }

        /// 반환값 = 실제로 담긴 수량. amount보다 적으면 그만큼이 넘친 것이다.
        ///
        /// 넘친 분을 어떻게 할지는 호출하는 쪽이 정한다. 돌려주는 자리(제작 환불·수령)는
        /// DropService.GiveOrDrop을 거쳐 바닥에 떨구고, 사는 자리는 SpaceFor로 미리 막는다.
        /// ⚠️ 그냥 Add만 부르고 반환값을 버리면 그 수량이 조용히 증발한다.
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

        // ---------------- 저장 ----------------

        [Serializable]
        class SavedStack
        {
            public string Item;
            public int Count;
        }

        [Serializable]
        class Save
        {
            public int SlotCount;
            public List<SavedStack> Stacks = new List<SavedStack>();
        }

        public string SaveKey => "inventory";

        public string Capture()
        {
            var save = new Save { SlotCount = _slotCount };
            for (int i = 0; i < _stacks.Count; i++)
            {
                save.Stacks.Add(new SavedStack
                {
                    Item = SaveCatalog.NameOf(_stacks[i].Definition),
                    Count = _stacks[i].Count,
                });
            }
            return JsonUtility.ToJson(save);
        }

        public void Restore(string payload)
        {
            var save = JsonUtility.FromJson<Save>(payload);
            if (save == null) return;

            _slotCount = Mathf.Max(1, save.SlotCount);
            _stacks.Clear();

            for (int i = 0; i < save.Stacks.Count; i++)
            {
                SavedStack saved = save.Stacks[i];

                // 에셋이 사라졌으면 그 칸만 버린다. 저장 전체를 거부하면 아이템 하나 지웠다고
                // 세이브가 통째로 죽어서 개발이 막힌다.
                ItemDefinition definition = _catalog != null ? _catalog.Find<ItemDefinition>(saved.Item) : null;
                if (definition == null) continue;

                _stacks.Add(new ItemStack(definition, saved.Count));
            }

            Changed?.Invoke(this);
        }
    }
}
