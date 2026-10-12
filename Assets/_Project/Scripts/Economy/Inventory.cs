using System;
using System.Collections.Generic;
using UnityEngine;

namespace ForagerCP
{
    /// 플레이어 가방. 칸·스택 규칙 자체는 ItemContainer가 들고 있고 여기는 그 껍데기다 —
    /// 보관함(I6)이 같은 규칙을 쓰기 때문에 규칙을 컴포넌트에 묶어둘 수 없다.
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

        public event Action<Inventory> Changed;

        ItemContainer _items;

        /// 지연 생성: 인벤을 Awake 전에 건드리는 쪽(다른 컴포넌트의 Awake, 에디터 도구)이 있다.
        /// 그때 null이면 조용히 0개로 보여 '왜 안 들어가지'를 추적하게 된다.
        public ItemContainer Items
        {
            get
            {
                if (_items != null) return _items;

                _items = new ItemContainer(_slotCount, _enforceCapacity);
                _items.Changed += () => Changed?.Invoke(this);
                return _items;
            }
        }

        public IReadOnlyList<ItemStack> Stacks => Items.Stacks;
        public int SlotCount => Items.SlotCount;
        public bool EnforceCapacity => Items.EnforceCapacity;
        public int UsedSlots => Items.UsedSlots;
        public int TotalCount => Items.TotalCount;

        void Awake() => _ = Items;

        void Start() => Changed?.Invoke(this);

        public int CountOf(ItemDefinition definition) => Items.CountOf(definition);

        /// 지금 이 종류를 몇 개까지 받을 수 있는지. 사는 쪽은 돈을 내기 전에 이걸 물어야 한다.
        public int SpaceFor(ItemDefinition definition) => Items.SpaceFor(definition);

        /// 반환값 = 실제로 담긴 수량. amount보다 적으면 그만큼이 넘친 것이다.
        ///
        /// 넘친 분을 어떻게 할지는 호출하는 쪽이 정한다. 돌려주는 자리(제작 환불·수령)는
        /// DropService.GiveOrDrop을 거쳐 바닥에 떨구고, 사는 자리는 SpaceFor로 미리 막는다.
        /// ⚠️ 그냥 Add만 부르고 반환값을 버리면 그 수량이 조용히 증발한다.
        public int Add(ItemDefinition definition, int amount) => Items.Add(definition, amount);

        /// 반환값 = 실제로 뺀 수량.
        public int Remove(ItemDefinition definition, int amount) => Items.Remove(definition, amount);

        /// 전량 꺼내기(변환기). 꺼낸 목록을 그대로 돌려주고 가방은 빈다.
        public List<ItemStack> TakeAll() => Items.TakeAll();

        public void Clear() => Items.Clear();

        /// 칸 수 확장(상점 가방 확장). 줄이는 건 넘치는 칸 처리 규칙이 없어 막아둔다.
        public void ExpandSlots(int extraSlots) => Items.ExpandSlots(extraSlots);

        // ---------------- 저장 ----------------

        public string SaveKey => "inventory";

        public string Capture() => Items.Capture();

        public void Restore(string payload) => Items.Restore(payload, _catalog);
    }
}
