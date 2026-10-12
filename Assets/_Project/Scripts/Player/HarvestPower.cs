using System;
using UnityEngine;

namespace ForagerCP
{
    /// 채집 공격력(기본 5). 상점 아이템 효과가 건드리는 유일한 지점이라
    /// 성장 요소가 늘어도 수정 범위가 여기로 묶인다.
    public class HarvestPower : MonoBehaviour, ISaveable
    {
        void Awake()
        {
            if (_equipment == null) _equipment = FindFirstObjectByType<PlayerEquipment>();
            if (_buffs == null) _buffs = FindFirstObjectByType<PlayerBuffs>();
        }

        [SerializeField] int _basePower = 5;
        [SerializeField] PlayerEquipment _equipment;
        [SerializeField] PlayerBuffs _buffs;

        public event Action<int> Changed;

        /// 스킬·상점이 올린 몫. 도구에서 나오는 몫과 따로 둔다 —
        /// 섞으면 불러올 때 도구 수치가 두 번 들어간다(장비도 따로 저장하므로).
        public int Bonus { get; private set; }

        public int FromTool => _equipment != null ? _equipment.HarvestPower : 0;
        public int FromBuff => _buffs != null ? _buffs.HarvestPower : 0;
        public int Value => _basePower + FromTool + FromBuff + Bonus;

        public void AddBonus(int amount)
        {
            if (amount == 0) return;
            Bonus += amount;
            Changed?.Invoke(Value);
        }

        // ---------------- 저장 ----------------

        [Serializable]
        class Save
        {
            public int Bonus;
        }

        public string SaveKey => "harvestPower";

        public string Capture() => JsonUtility.ToJson(new Save { Bonus = Bonus });

        public void Restore(string payload)
        {
            var save = JsonUtility.FromJson<Save>(payload);
            if (save == null) return;

            Bonus = save.Bonus;
            Changed?.Invoke(Value);
        }
    }
}
