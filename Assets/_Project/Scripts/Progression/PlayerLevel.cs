using System;
using UnityEngine;

namespace ForagerCP
{
    /// 필요 경험치 = _expBase + 현재 레벨 (Lv1→2: 6, Lv2→3: 7 ...). 사양 1-5.
    public class PlayerLevel : MonoBehaviour
    {
        [SerializeField] int _startLevel = 1;
        [SerializeField] int _maxLevel = 10;
        [SerializeField] int _expBase = 5;

        public event Action<PlayerLevel> Changed;

        public int Level { get; private set; }
        public int Exp { get; private set; }
        public int MaxLevel => _maxLevel;
        public bool IsMaxLevel => Level >= _maxLevel;

        /// 최대 레벨에서는 다음 요구치가 의미 없으므로 0을 돌려주고 UI가 '-'로 찍는다.
        public int ExpToNext => IsMaxLevel ? 0 : _expBase + Level;

        void Awake() => Level = Mathf.Max(1, _startLevel);

        void Start() => Changed?.Invoke(this);

        public void AddExp(int amount)
        {
            if (amount <= 0) return;
            Exp += amount;

            // while: 한 번에 여러 레벨 조건을 넘기면 연속 레벨업, 초과분은 그대로 이월(사양 1-5).
            while (!IsMaxLevel && Exp >= ExpToNext)
            {
                Exp -= ExpToNext;
                Level++;
            }

            Changed?.Invoke(this);
        }

        /// 디버그 전용(사양 1-11). 경험치와 무관하게 레벨만 올린다.
        public void DebugAddLevel(int amount)
        {
            if (amount <= 0) return;
            Level = Mathf.Min(_maxLevel, Level + amount);
            Changed?.Invoke(this);
        }
    }
}
