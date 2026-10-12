using System;
using System.Collections.Generic;
using UnityEngine;

namespace ForagerCP
{
    /// 일정 시간 동안 걸리는 강화 하나.
    ///
    /// 수치를 장비와 같은 넷으로 맞춘 이유: 합산하는 자리가 늘어나면 안 된다.
    /// 공격력을 쓰는 쪽이 '장비 + 버프'만 더하면 되게 둔다.
    [CreateAssetMenu(menuName = "ForagerCP/버프", fileName = "Buff")]
    public class BuffDefinition : ScriptableObject
    {
        [SerializeField] string _displayName;
        [SerializeField] Color _uiColor = new Color(0.72f, 0.55f, 0.95f);
        [SerializeField] float _seconds = 60f;

        [Header("수치")]
        [SerializeField] int _harvestPower;
        [SerializeField] int _attackPower;
        [SerializeField] int _damageReduction;

        public string DisplayName => string.IsNullOrEmpty(_displayName) ? name : _displayName;
        public Color UiColor => _uiColor;
        public float Seconds => Mathf.Max(1f, _seconds);

        public int HarvestPower => _harvestPower;
        public int AttackPower => _attackPower;
        public int DamageReduction => _damageReduction;

        public string Describe()
        {
            var parts = new List<string>();
            if (_harvestPower != 0) parts.Add($"채집 +{_harvestPower}");
            if (_attackPower != 0) parts.Add($"공격 +{_attackPower}");
            if (_damageReduction != 0) parts.Add($"피해 감소 {_damageReduction}");

            string what = parts.Count == 0 ? "효과 없음" : string.Join(" · ", parts);
            return $"{what} ({_seconds:0}초)";
        }
    }

    /// 지금 걸려 있는 버프와 남은 시간.
    ///
    /// 같은 버프를 다시 마시면 쌓지 않고 시간만 채운다 — 겹쳐 쌓는 규칙은 기획 미확정이고,
    /// 쌓이는 쪽이 되돌리기 어렵기 때문.
    public class PlayerBuffs : MonoBehaviour, ISaveable
    {
        [SerializeField] SaveCatalog _catalog;

        public event Action<PlayerBuffs> Changed;

        class Active
        {
            public BuffDefinition Buff;
            public float Remaining;
        }

        readonly List<Active> _active = new List<Active>();

        public int Count => _active.Count;

        public BuffDefinition BuffAt(int index) => index >= 0 && index < _active.Count ? _active[index].Buff : null;
        public float RemainingAt(int index) => index >= 0 && index < _active.Count ? _active[index].Remaining : 0f;

        public int HarvestPower => Sum(buff => buff.HarvestPower);
        public int AttackPower => Sum(buff => buff.AttackPower);
        public int DamageReduction => Sum(buff => buff.DamageReduction);

        int Sum(Func<BuffDefinition, int> pick)
        {
            int total = 0;
            for (int i = 0; i < _active.Count; i++)
            {
                if (_active[i].Buff != null) total += pick(_active[i].Buff);
            }
            return total;
        }

        public void Apply(BuffDefinition buff)
        {
            if (buff == null) return;

            for (int i = 0; i < _active.Count; i++)
            {
                if (_active[i].Buff != buff) continue;

                _active[i].Remaining = buff.Seconds;
                Changed?.Invoke(this);
                return;
            }

            _active.Add(new Active { Buff = buff, Remaining = buff.Seconds });
            Changed?.Invoke(this);
        }

        void Update()
        {
            if (_active.Count == 0) return;

            bool dropped = false;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                _active[i].Remaining -= Time.deltaTime;
                if (_active[i].Remaining > 0f) continue;

                _active.RemoveAt(i);
                dropped = true;
            }

            // 매 프레임 알리면 듣는 쪽이 매 프레임 다시 그린다. 끝났을 때만 알린다.
            if (dropped) Changed?.Invoke(this);
        }

        // ---------------- 저장 ----------------

        [Serializable]
        class SavedBuff
        {
            public string Buff;
            public float Remaining;
        }

        [Serializable]
        class Save
        {
            public List<SavedBuff> Active = new List<SavedBuff>();
        }

        public string SaveKey => "buffs";

        public string Capture()
        {
            var save = new Save();
            for (int i = 0; i < _active.Count; i++)
            {
                save.Active.Add(new SavedBuff
                {
                    Buff = SaveCatalog.NameOf(_active[i].Buff),

                    // 초 단위로 깎는다. 소수점까지 적으면 매 프레임 글자가 달라져
                    // '바뀐 게 있나' 비교가 늘 참이 된다.
                    Remaining = Mathf.Ceil(_active[i].Remaining),
                });
            }

            save.Active.Sort((a, b) => string.CompareOrdinal(a.Buff, b.Buff));
            return JsonUtility.ToJson(save);
        }

        public void Restore(string payload)
        {
            var save = JsonUtility.FromJson<Save>(payload);
            if (save == null) return;

            _active.Clear();
            for (int i = 0; i < save.Active.Count; i++)
            {
                BuffDefinition buff = _catalog != null
                    ? _catalog.Find<BuffDefinition>(save.Active[i].Buff)
                    : null;

                if (buff == null) continue;
                _active.Add(new Active { Buff = buff, Remaining = Mathf.Max(0.1f, save.Active[i].Remaining) });
            }

            Changed?.Invoke(this);
        }
    }
}
