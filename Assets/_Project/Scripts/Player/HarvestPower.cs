using System;
using UnityEngine;

namespace ForagerCP
{
    /// 채집 공격력(기본 5). 상점 아이템 효과가 건드리는 유일한 지점이라
    /// 성장 요소가 늘어도 수정 범위가 여기로 묶인다.
    public class HarvestPower : MonoBehaviour
    {
        [SerializeField] int _basePower = 5;

        public event Action<int> Changed;

        public int Bonus { get; private set; }
        public int Value => _basePower + Bonus;

        public void AddBonus(int amount)
        {
            if (amount == 0) return;
            Bonus += amount;
            Changed?.Invoke(Value);
        }
    }
}
