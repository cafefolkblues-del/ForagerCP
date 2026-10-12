using System;
using System.Collections.Generic;
using UnityEngine;

namespace ForagerCP
{
    /// 스킬 진행 상태. 포인트가 얼마나 있고 무엇을 배웠는지.
    ///
    /// 확정 규칙
    ///  - 레벨이 1 오를 때마다 포인트 1
    ///  - 처음에는 격자가 정한 시작 노드만 고를 수 있다
    ///  - 하나를 배우면 그 노드와 맞닿은 칸이 고를 수 있게 열린다(공짜로 배워지는 것은 아니다)
    ///
    /// RegionAccess와 같은 자리다 — 정의는 에셋, 진행은 여기.
    public class SkillBoard : MonoBehaviour, ISaveable, ISaveMilestone
    {
        [SerializeField] SkillTree _tree;
        [SerializeField] PlayerLevel _level;

        [Header("효과가 건드릴 곳")]
        [SerializeField] HarvestPower _harvest;
        [SerializeField] PlayerHealth _health;
        [SerializeField] ProgressFlags _flags;
        [SerializeField] Inventory _inventory;

        /// 레벨 1칸당 받는 포인트.
        [SerializeField] int _pointsPerLevel = 1;

        /// 시작 포인트. 테스트용으로 열어둔다.
        [SerializeField] int _startingPoints;

        [SerializeField] SaveCatalog _catalog;

        public event Action<SkillBoard> Changed;
        public event Action<SkillDefinition> Learned;

        /// 포인트를 치르고 배우는 일이라 되돌릴 수 없다.
        public event Action<string> Milestone;

        /// 포인트가 모자라거나 아직 안 열린 노드를 눌렀을 때. 화면이 이유를 띄운다.
        public event Action<SkillDefinition, string> Refused;

        public SkillTree Tree => _tree;
        public int Points { get; private set; }
        public int Spent { get; private set; }

        readonly HashSet<SkillDefinition> _learned = new HashSet<SkillDefinition>();
        readonly List<SkillDefinition> _neighbours = new List<SkillDefinition>();

        SkillContext _context;
        int _lastLevel;

        /// 레벨을 한 번이라도 들은 적 있는지. 첫 보고는 '시작 상태'라 포인트를 주면 안 된다.
        bool _levelKnown;

        /// RegionAccess에서 겪은 것과 같은 이유로 늦게 만든다 —
        /// Awake에서만 채우면 에디터 도구가 물었을 때 null이라 그대로 터진다.
        SkillContext Context
        {
            get
            {
                if (_context != null) return _context;

                if (_harvest == null) _harvest = FindFirstObjectByType<HarvestPower>();
                if (_health == null) _health = FindFirstObjectByType<PlayerHealth>();
                if (_flags == null) _flags = FindFirstObjectByType<ProgressFlags>();
                if (_inventory == null) _inventory = FindFirstObjectByType<Inventory>();

                _context = new SkillContext
                {
                    Harvest = _harvest,
                    Health = _health,
                    Flags = _flags,
                    Inventory = _inventory,
                };
                return _context;
            }
        }

        void Awake()
        {
            if (_level == null) _level = FindFirstObjectByType<PlayerLevel>();

            Points = Mathf.Max(0, _startingPoints);
            _ = Context;
        }

        void OnEnable()
        {
            if (_level != null) _level.Changed += OnLevelChanged;
        }

        void OnDisable()
        {
            if (_level != null) _level.Changed -= OnLevelChanged;
        }

        void Start() => Changed?.Invoke(this);

        /// Changed는 경험치만 올라도 오므로 레벨이 실제로 몇 칸 올랐는지 직접 센다.
        ///
        /// 첫 보고에서는 아무것도 주지 않고 기준만 잡는다. Awake 순서가 PlayerLevel보다 앞이면
        /// 그쪽 Level이 아직 0이라, 시작하자마자 레벨 1칸을 올린 것으로 세어 포인트가 하나 생긴다.
        void OnLevelChanged(PlayerLevel level)
        {
            if (!_levelKnown)
            {
                _levelKnown = true;
                _lastLevel = level.Level;
                return;
            }

            int gained = level.Level - _lastLevel;
            _lastLevel = level.Level;
            if (gained <= 0) return;

            AddPoints(gained * Mathf.Max(0, _pointsPerLevel));
        }

        public void AddPoints(int amount)
        {
            if (amount <= 0) return;

            Points += amount;
            Changed?.Invoke(this);
        }

        public bool IsLearned(SkillDefinition skill) => skill != null && _learned.Contains(skill);

        /// 고를 수 있는가. 시작 노드이거나, 이미 배운 노드와 맞닿아 있으면 열린다.
        public bool IsAvailable(SkillDefinition skill)
        {
            if (skill == null || _tree == null) return false;
            if (_learned.Contains(skill)) return false;
            if (_tree.IsStarting(skill)) return true;

            _tree.CollectNeighbours(skill, _neighbours);
            for (int i = 0; i < _neighbours.Count; i++)
            {
                if (_learned.Contains(_neighbours[i])) return true;
            }
            return false;
        }

        /// 지금 배울 수 있는가. reason은 못 배울 때만 채워진다.
        public bool CanLearn(SkillDefinition skill, out string reason)
        {
            reason = null;
            if (skill == null) { reason = "스킬이 없습니다"; return false; }

            if (_learned.Contains(skill)) { reason = "이미 배웠습니다"; return false; }
            if (!IsAvailable(skill)) { reason = "이웃한 스킬을 먼저 배워야 합니다"; return false; }
            if (Points < skill.Cost) { reason = $"스킬 포인트 {skill.Cost} 필요"; return false; }

            return true;
        }

        public bool TryLearn(SkillDefinition skill)
        {
            if (!CanLearn(skill, out string reason))
            {
                Refused?.Invoke(skill, reason);
                return false;
            }

            // 먼저 치르고 나중에 적용한다. 반대로 하면 효과만 들어가고 포인트가 안 빠지는 순간이 생긴다.
            Points -= skill.Cost;
            Spent += skill.Cost;
            _learned.Add(skill);

            skill.Apply(Context);

            Learned?.Invoke(skill);
            Changed?.Invoke(this);
            Milestone?.Invoke($"스킬: {skill.DisplayName}");
            return true;
        }

        /// 디버그·테스트용. 효과는 되돌리지 않는다(효과마다 되돌리는 법이 달라서,
        /// 되감기가 필요해지면 SkillEffect에 Revert를 열어야 한다).
        public void ResetLearned()
        {
            if (_learned.Count == 0) return;

            Points += Spent;
            Spent = 0;
            _learned.Clear();
            Changed?.Invoke(this);
        }

        // ---------------- 저장 ----------------

        [Serializable]
        class Save
        {
            public int Points;
            public int Spent;
            public int LastLevel;
            public List<string> Learned = new List<string>();
        }

        public string SaveKey => "skills";

        public string Capture()
        {
            var save = new Save { Points = Points, Spent = Spent, LastLevel = _lastLevel };
            foreach (SkillDefinition skill in _learned) save.Learned.Add(SaveCatalog.NameOf(skill));

            // 집합은 도는 순서를 보장하지 않는다. 정렬해야 같은 상태가 늘 같은 글자로 나온다.
            save.Learned.Sort(StringComparer.Ordinal);
            return JsonUtility.ToJson(save);
        }

        public void Restore(string payload)
        {
            var save = JsonUtility.FromJson<Save>(payload);
            if (save == null) return;

            Points = Mathf.Max(0, save.Points);
            Spent = Mathf.Max(0, save.Spent);

            // 불러온 레벨로 기준을 다시 잡는다. 안 하면 복원 직후 레벨 차이만큼 포인트가 또 들어온다.
            _lastLevel = save.LastLevel;
            _levelKnown = true;

            _learned.Clear();
            for (int i = 0; i < save.Learned.Count; i++)
            {
                SkillDefinition skill = _catalog != null ? _catalog.Find<SkillDefinition>(save.Learned[i]) : null;
                if (skill != null) _learned.Add(skill);
            }

            // 효과는 다시 걸지 않는다. 효과가 올린 수치(채집력 등)는 그 시스템이 따로 저장하므로
            // 여기서 또 적용하면 두 번 들어간다.
            Changed?.Invoke(this);
        }
    }
}
