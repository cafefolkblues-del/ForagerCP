using System;
using System.Collections.Generic;
using UnityEngine;

namespace ForagerCP
{
    /// 지금 끼고 있는 장비와 거기서 나오는 수치.
    ///
    /// 수치를 쓰는 쪽(공격력·채집력·피해감소·최대체력)이 전부 여기만 보게 한다.
    /// 장비 종류마다 컴포넌트를 두면 수치를 합치는 곳이 흩어져, 세트 효과 같은 게 들어올 때 샌다.
    ///
    /// 장비는 인벤토리에 들어가지 않는다(확정: 되팔기·보관 불가). 낀 것이 곧 가진 것이다.
    public class PlayerEquipment : MonoBehaviour, ISaveable
    {
        [SerializeField] SaveCatalog _catalog;

        /// 시작할 때 끼고 있을 것들. 비워두면 맨손.
        [SerializeField] List<EquipmentDefinition> _startingGear = new List<EquipmentDefinition>();

        public event Action<PlayerEquipment> Changed;

        readonly Dictionary<EquipmentSlot, EquipmentDefinition> _worn =
            new Dictionary<EquipmentSlot, EquipmentDefinition>();

        void Awake()
        {
            for (int i = 0; i < _startingGear.Count; i++)
            {
                if (_startingGear[i] != null) _worn[_startingGear[i].Slot] = _startingGear[i];
            }
        }

        void Start() => Changed?.Invoke(this);

        public EquipmentDefinition Worn(EquipmentSlot slot)
            => _worn.TryGetValue(slot, out EquipmentDefinition found) ? found : null;

        /// 낀 것이 없으면 0. 구매·제작 규칙이 "지금보다 위인가"를 이 값으로 판단한다.
        public int TierOf(EquipmentSlot slot)
        {
            EquipmentDefinition worn = Worn(slot);
            return worn != null ? worn.Tier : 0;
        }

        public string NameOf(EquipmentSlot slot)
        {
            EquipmentDefinition worn = Worn(slot);
            return worn != null ? worn.DisplayName : "없음";
        }

        // ---------------- 합산 수치 ----------------

        public int HarvestPower => Sum(equipment => equipment.HarvestPower);
        public int AttackPower => Sum(equipment => equipment.AttackPower);
        public int DamageReduction => Sum(equipment => equipment.DamageReduction);
        public int MaxHpBonus => Sum(equipment => equipment.MaxHpBonus);

        int Sum(Func<EquipmentDefinition, int> pick)
        {
            int total = 0;
            foreach (EquipmentDefinition equipment in _worn.Values)
            {
                if (equipment != null) total += pick(equipment);
            }
            return total;
        }

        // ---------------- 끼우기 ----------------

        /// 확정 규칙(M4): 중복 불가 / 등급 건너뛰기 허용 / 하향 교체 금지.
        /// 셋이 전부 "지금 낀 것보다 위인가" 하나로 정리된다.
        public bool CanEquip(EquipmentDefinition equipment, out string reason)
        {
            reason = null;
            if (equipment == null) { reason = "장비가 없습니다"; return false; }

            int current = TierOf(equipment.Slot);
            if (equipment.Tier == current) { reason = "이미 끼고 있습니다"; return false; }
            if (equipment.Tier < current) { reason = "지금 낀 것보다 낮습니다"; return false; }

            return true;
        }

        public bool Equip(EquipmentDefinition equipment)
        {
            if (!CanEquip(equipment, out _)) return false;

            // 예전 것은 그냥 사라진다. 되팔기도 보관도 안 되므로 돌려줄 곳이 없다.
            _worn[equipment.Slot] = equipment;

            Changed?.Invoke(this);
            return true;
        }

        // ---------------- 저장 ----------------

        [Serializable]
        class Save
        {
            public List<string> Worn = new List<string>();
        }

        public string SaveKey => "equipment";

        public string Capture()
        {
            var save = new Save();
            foreach (EquipmentDefinition equipment in _worn.Values)
            {
                if (equipment != null) save.Worn.Add(SaveCatalog.NameOf(equipment));
            }

            // 사전은 도는 순서를 보장하지 않는다. 정렬해야 같은 상태가 늘 같은 글자로 나온다.
            save.Worn.Sort(StringComparer.Ordinal);
            return JsonUtility.ToJson(save);
        }

        public void Restore(string payload)
        {
            var save = JsonUtility.FromJson<Save>(payload);
            if (save == null) return;

            _worn.Clear();
            for (int i = 0; i < save.Worn.Count; i++)
            {
                EquipmentDefinition equipment = _catalog != null
                    ? _catalog.Find<EquipmentDefinition>(save.Worn[i])
                    : null;

                // 장비가 지워졌으면 그 자리만 빈다. Equip을 거치지 않는 이유는
                // 복원은 규칙을 다시 따질 일이 아니라 적힌 그대로 되돌리는 일이기 때문.
                if (equipment != null) _worn[equipment.Slot] = equipment;
            }

            Changed?.Invoke(this);
        }
    }
}
