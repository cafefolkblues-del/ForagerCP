using System;
using System.Collections.Generic;
using UnityEngine;

namespace ForagerCP
{
    /// 칸 + 스택 규칙 그 자체. MonoBehaviour가 아니라 순수 클래스인 이유:
    /// 같은 규칙을 쓰는 곳이 둘 이상이다(플레이어 가방, 보관함). 규칙이 두 군데 있으면
    /// 한쪽만 고치는 사고가 난다 — 넘침 처리를 정리할 때 이미 겪은 일이다.
    ///
    /// 들고 있는 쪽이 누구인지는 모른다. 바뀌었다는 사실만 알리고, 그걸 자기 이벤트로
    /// 다시 쏘는 일은 들고 있는 쪽(Inventory, StorageBox)이 한다.
    public class ItemContainer
    {
        readonly List<ItemStack> _stacks = new List<ItemStack>();

        int _slotCount;
        bool _enforceCapacity;

        public event Action Changed;

        public ItemContainer(int slotCount, bool enforceCapacity = true)
        {
            _slotCount = Mathf.Max(1, slotCount);
            _enforceCapacity = enforceCapacity;
        }

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
        /// ⚠️ 반환값을 버리면 그 수량이 조용히 증발한다.
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
            if (accepted > 0) Changed?.Invoke();
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
            if (removed > 0) Changed?.Invoke();
            return removed;
        }

        /// 다른 칸으로 옮긴다. 반환값 = 실제로 옮긴 수량.
        ///
        /// 받는 쪽에 먼저 넣고 들어간 만큼만 뺀다. 반대로 하면 받는 쪽이 꽉 찼을 때
        /// 뺀 분이 갈 곳 없이 사라진다 — 가방↔보관함은 양쪽이 다 꽉 찰 수 있는 자리다.
        public int MoveTo(ItemContainer other, ItemDefinition definition, int amount)
        {
            if (other == null || other == this || definition == null || amount <= 0) return 0;

            int available = Mathf.Min(amount, CountOf(definition));
            if (available <= 0) return 0;

            int moved = other.Add(definition, available);
            if (moved > 0) Remove(definition, moved);
            return moved;
        }

        /// 전량 꺼내기(변환기). 꺼낸 목록을 그대로 돌려주고 여기는 빈다.
        public List<ItemStack> TakeAll()
        {
            var taken = new List<ItemStack>(_stacks);
            if (taken.Count == 0) return taken;

            _stacks.Clear();
            Changed?.Invoke();
            return taken;
        }

        public void Clear()
        {
            if (_stacks.Count == 0) return;

            _stacks.Clear();
            Changed?.Invoke();
        }

        /// 칸 수 확장(상점 가방 확장). 줄이는 건 넘치는 칸 처리 규칙이 없어 막아둔다.
        public void ExpandSlots(int extraSlots)
        {
            if (extraSlots <= 0) return;

            _slotCount += extraSlots;
            Changed?.Invoke();
        }

        // ---------------- 저장 ----------------
        //
        // 저장 모양도 여기 둔다. 가방과 보관함이 같은 글자를 쓰게 해야
        // 한쪽 모양만 바뀌어 다른 쪽 세이브가 조용히 깨지는 일이 없다.

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

        public void Restore(string payload, SaveCatalog catalog)
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
                ItemDefinition definition = catalog != null ? catalog.Find<ItemDefinition>(saved.Item) : null;
                if (definition == null) continue;

                _stacks.Add(new ItemStack(definition, saved.Count));
            }

            Changed?.Invoke();
        }
    }
}
