using UnityEngine;

namespace ForagerCP
{
    /// 누구를 쫓는가, 아직 쫓아도 되는가. 표적·탐지·구역·끈(leash) 판정을 한 곳에 둔다.
    ///
    /// 몹마다 바꿀 수 없게 공통으로 두는 이유: "다른 구역의 플레이어는 표적이 아니다"는
    /// 차단벽 너머 어그로 버그(3차 피드백 1번)를 고친 결과다. 몹별 상태가 각자 판정하면
    /// 한 몹만 빠뜨려도 그 몹만 벽 너머로 쫓아온다. 이동은 하지 않는다(MonsterMotor 몫).
    public class MonsterAggro
    {
        readonly Monster _monster;

        RegionDefinition _homeRegion;
        bool _homeRegionKnown;

        public MonsterAggro(Monster monster) => _monster = monster;

        public Transform Target { get; private set; }
        public IDamageable TargetDamageable { get; private set; }
        public bool HasTarget => Target != null;

        public void Acquire(Transform target)
        {
            Target = target;
            TargetDamageable = target != null ? target.GetComponent<IDamageable>() : null;
        }

        /// 이 몬스터가 서 있는 구역. 구획 밖에 놓였으면 null이고, 그때는 구역 규칙을 적용하지 않는다.
        ///
        /// 처음에 한 번 잡지 않는 이유: 스폰 자리는 같은 오브젝트의 Monster.Start가 세우는데
        /// 같은 오브젝트 안의 Start 순서는 보장되지 않는다. 먼저 읽으면 원점(0,0,0)의 구역을 본다.
        /// 값이 설 때까지 다시 묻고, 한 번 잡히면 그대로 둔다.
        public RegionDefinition HomeRegion
        {
            get
            {
                if (_homeRegionKnown) return _homeRegion;
                if (_monster == null) return null;

                _homeRegion = RegionMap.At(_monster.HomePosition);
                if (_homeRegion != null) _homeRegionKnown = true;
                return _homeRegion;
            }
        }

        /// 표적까지의 평면 벡터. 높이 차이를 섞으면 같은 칸인데 지형에 따라 거리가 달라진다.
        public Vector3 ToTarget(Vector3 from)
        {
            Vector3 v = Target.position - from;
            v.y = 0f;
            return v;
        }

        public Vector3 ToHome(Vector3 from)
        {
            Vector3 v = _monster.HomePosition - from;
            v.y = 0f;
            return v;
        }

        /// 확장 2-2: 지금은 스폰 지점 기준 거리로 대신한다.
        /// 구역(아일랜드) 개념이 들어오면 "플레이어가 구역을 넘었는가"로 바꾼다.
        public bool PastLeash(Vector3 toHome, float leashDistance) => toHome.magnitude > leashDistance;

        /// 탐지 거리 밖이거나 다른 구역이면 표적을 놓친 것이다.
        ///
        /// 시야 레이캐스트를 쓰지 않은 이유: 광물·구조물 뒤에 선 것까지 놓쳐서
        /// 보고된 것보다 넓게 바뀌고, 구역 판정은 그런 오작동이 없다.
        public bool IsLost(float distanceToTarget, float detectRange)
            => distanceToTarget > detectRange || !TargetInSameRegion();

        /// 구획되지 않은 땅에 놓인 몬스터는 이 규칙을 적용하지 않는다 —
        /// 구역이 없는데 '같은 구역'을 요구하면 영영 아무도 쫓지 않는다.
        public bool TargetInSameRegion()
        {
            RegionDefinition home = HomeRegion;
            if (home == null) return true;
            return RegionMap.At(Target.position) == home;
        }
    }
}
