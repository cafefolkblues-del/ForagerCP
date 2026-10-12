using System;
using System.Collections.Generic;
using UnityEngine;

namespace ForagerCP
{
    /// "여기 들어갈 수 있나"를 묻는 단일 창구. 문도 UI도 되돌려보내기도 전부 여기에만 묻는다.
    ///
    /// 해금 여부는 런타임 상태라 지역 에셋이 아니라 여기가 들고 있다.
    /// ScriptableObject에 담으면 에디터에서 연 상태가 저장돼 다음 플레이에 그대로 남는다.
    public class RegionAccess : MonoBehaviour
    {
        [SerializeField] PlayerLevel _level;
        [SerializeField] Inventory _inventory;
        [SerializeField] GoldWallet _gold;
        [SerializeField] ProgressFlags _flags;

        public event Action<RegionDefinition> Unlocked;

        /// 조건을 못 채워 막혔을 때. 인자는 걸린 조건 — 화면에 이유를 그대로 띄울 수 있다.
        public event Action<RegionDefinition, RegionRequirement> Refused;

        readonly HashSet<RegionDefinition> _unlocked = new HashSet<RegionDefinition>();
        RegionContext _context;

        /// 늦게 만든다. Awake에서만 채우면 에디터 도구가 물었을 때 null이 되는데,
        /// 조건은 context를 믿고 쓰므로 그대로 터진다(문 안내 문구를 씬에서 미리 볼 때 실제로 터졌다).
        RegionContext Context
        {
            get
            {
                if (_context != null) return _context;

                if (_level == null) _level = FindFirstObjectByType<PlayerLevel>();
                if (_inventory == null) _inventory = FindFirstObjectByType<Inventory>();
                if (_gold == null) _gold = FindFirstObjectByType<GoldWallet>();
                if (_flags == null) _flags = FindFirstObjectByType<ProgressFlags>();

                _context = new RegionContext
                {
                    Level = _level,
                    Inventory = _inventory,
                    Gold = _gold,
                    Flags = _flags,
                };
                return _context;
            }
        }

        void Awake()
        {
            // 참조를 여기서 한 번 확정해 둔다. 실제 조립은 Context가 한다.
            _ = Context;
        }

        /// 레벨이 오르거나 사건이 일어나면 그때만 다시 따져본다.
        /// 매 프레임 전부 확인하면 조건 수 x 지역 수가 계속 돌고, 늦게 반영되면 문 앞에서 멈춰 선다.
        void OnEnable()
        {
            if (_level != null) _level.Changed += OnLevelChanged;
            if (_flags != null) _flags.Raised += OnFlagRaised;
        }

        void OnDisable()
        {
            if (_level != null) _level.Changed -= OnLevelChanged;
            if (_flags != null) _flags.Raised -= OnFlagRaised;
        }

        // 볼륨들이 자기 등록을 마친 뒤라야 지역 목록이 채워진다. 그래서 Awake가 아니라 Start.
        void Start() => ReevaluateAll();

        void OnLevelChanged(PlayerLevel level) => ReevaluateAll();
        void OnFlagRaised(string flag) => ReevaluateAll();

        /// 조건이 없는 곳(시작 지역, 구획 안 한 땅)은 언제나 열려 있다.
        public bool IsUnlocked(RegionDefinition region)
        {
            if (region == null || !region.IsLockable) return true;
            return region.PermanentUnlock && _unlocked.Contains(region);
        }

        /// 지금 조건을 만족하는지만 본다. 아무것도 치르지 않는다 — 문 앞 안내 문구가 이걸 쓴다.
        public bool CanEnter(RegionDefinition region, out RegionRequirement blocking)
        {
            blocking = null;
            if (region == null || !region.IsLockable) return true;
            if (IsUnlocked(region)) return true;

            IReadOnlyList<RegionRequirement> requirements = region.Requirements;
            for (int i = 0; i < requirements.Count; i++)
            {
                RegionRequirement requirement = requirements[i];
                if (requirement == null || requirement.IsMet(Context)) continue;

                blocking = requirement;
                return false;
            }
            return true;
        }

        /// 실제로 열어본다. 조건을 다 채웠으면 치를 것을 치르고 연다.
        public bool TryUnlock(RegionDefinition region)
        {
            if (region == null || !region.IsLockable) return true;
            if (IsUnlocked(region)) return true;

            if (!CanEnter(region, out RegionRequirement blocking))
            {
                Refused?.Invoke(region, blocking);
                return false;
            }

            // 전부 통과한 뒤에 치른다. 하나씩 확인하며 치르면 중간에 막혔을 때 앞의 것만 빠져나간다.
            IReadOnlyList<RegionRequirement> requirements = region.Requirements;
            for (int i = 0; i < requirements.Count; i++)
            {
                if (requirements[i] != null) requirements[i].Consume(Context);
            }

            if (region.PermanentUnlock) _unlocked.Add(region);

            Unlocked?.Invoke(region);
            return true;
        }

        /// 조건이 바뀌었을 때(레벨업, 보스 처치) 문이 알아서 열리게 하려면 누군가 다시 물어야 한다.
        /// 매 프레임 전부 확인하는 대신, 바뀐 순간에만 이걸 부른다.
        public void ReevaluateAll()
        {
            var regions = new List<RegionDefinition>();
            RegionMap.CollectRegions(regions);

            for (int i = 0; i < regions.Count; i++)
            {
                RegionDefinition region = regions[i];
                if (!region.IsLockable || IsUnlocked(region)) continue;

                // 대가가 있는 지역은 저절로 열지 않는다. 그쪽은 플레이어가 문 앞에서 직접 열어야 한다.
                if (HasCost(region)) continue;
                if (!CanEnter(region, out _)) continue;

                TryUnlock(region);
            }
        }

        static bool HasCost(RegionDefinition region)
        {
            IReadOnlyList<RegionRequirement> requirements = region.Requirements;
            for (int i = 0; i < requirements.Count; i++)
            {
                if (requirements[i] != null && requirements[i].HasCost) return true;
            }
            return false;
        }
    }
}
