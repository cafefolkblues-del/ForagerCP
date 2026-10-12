using System;
using UnityEngine;

namespace ForagerCP
{
    /// 핫바에서 고른 소모품을 쓴다.
    ///
    /// 설치와 같은 E를 쓴다. 고른 것이 블럭이면 BlockPlacer가, 소모품이면 여기가 반응한다 —
    /// 한 아이템이 둘 다일 수는 없으므로 부딪히지 않고, 플레이어는 "E는 고른 것을 쓴다" 하나만 외우면 된다.
    public class ConsumableUser : MonoBehaviour
    {
        [SerializeField] GameInput _input;
        [SerializeField] QuickBar _quickBar;
        [SerializeField] Inventory _inventory;
        [SerializeField] PlayerHealth _health;
        [SerializeField] PlayerBuffs _buffs;

        public event Action<ConsumableDefinition> Used;

        /// 못 쓴 이유. 화면이 그대로 띄운다.
        public event Action<string> Refused;

        ConsumableContext _context;

        ConsumableContext Context
        {
            get
            {
                if (_context != null) return _context;

                if (_inventory == null) _inventory = FindFirstObjectByType<Inventory>();
                if (_health == null) _health = FindFirstObjectByType<PlayerHealth>();
                if (_buffs == null) _buffs = FindFirstObjectByType<PlayerBuffs>();

                _context = new ConsumableContext
                {
                    Health = _health,
                    Buffs = _buffs,
                    Inventory = _inventory,
                };
                return _context;
            }
        }

        void Awake()
        {
            if (_input == null) _input = FindFirstObjectByType<GameInput>();
            if (_quickBar == null) _quickBar = FindFirstObjectByType<QuickBar>();
            _ = Context;
        }

        void OnEnable()
        {
            if (_input != null) _input.PlacePressed += TryUse;
        }

        void OnDisable()
        {
            if (_input != null) _input.PlacePressed -= TryUse;
        }

        void TryUse()
        {
            // 고른 것이 소모품이 아니면 설치 쪽의 일이다. 조용히 넘긴다.
            if (_quickBar.SelectedItem is not ConsumableDefinition consumable) return;
            if (_quickBar.SelectedCount < consumable.CostPerUse)
            {
                Refused?.Invoke("모자랍니다");
                return;
            }

            if (!consumable.CanUse(Context, out string reason))
            {
                Refused?.Invoke(reason);
                return;
            }

            // 먼저 빼고 쓴다. 반대로 하면 효과만 들어가고 개수가 안 줄어드는 순간이 생긴다.
            if (_inventory.Remove(consumable, consumable.CostPerUse) < consumable.CostPerUse) return;

            consumable.Use(Context);
            Used?.Invoke(consumable);
        }
    }
}
