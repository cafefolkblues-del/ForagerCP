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
        /// 버프는 더 이상 위력을 올리지 않는다. 기획이 채집 버프를
        /// "광물 공격 간격 20% 감소"로 적어 **속도** 축으로 옮겼다(HarvestSpeedPercent).
        public int FromBuff => 0;

        /// 광물 공격 간격을 몇 % 줄이는가. 버프와 스킬이 더해진 값 —
        /// 출처마다 따로 곱하면 둘만 겹쳐도 간격이 거의 0이 된다.
        public int HarvestSpeedPercent => _buffs != null ? _buffs.HarvestSpeed : 0;
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
