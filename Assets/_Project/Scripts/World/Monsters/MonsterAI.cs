using System;
using UnityEngine;

namespace ForagerCP
{
    /// 몬스터의 이동·공격. 상태: 대기 → 추적 → 예고 → 공격 → 복귀.
    /// 경로 탐색은 아직 직선 추적이다 — 맵에 장애물이 드물어서 NavMesh를 굽는 비용이 더 크다.
    /// 슬라임이 구조물에 끼기 시작하면 NavMeshAgent로 갈아끼우면 되고, 그때 바꿀 곳은 MoveTowards 하나다.
    ///
    /// 무엇을 내지를지는 여기가 정하지 않는다 — MonsterDefinition이 든 공격 목록에서
    /// 지금 쓸 수 있는 것을 고를 뿐이다. 공격 종류가 늘어도 이 파일은 안 늘어난다.
    [RequireComponent(typeof(Monster))]
    public class MonsterAI : MonoBehaviour
    {
        public enum State { Idle, Chase, Windup, Attack, Return }

        [SerializeField] Transform _target;
        [SerializeField] Rigidbody _body;
        [SerializeField] Knockback _knockback;

        /// 이 높이 아래로 내려가면 맵을 뚫은 것으로 본다.
        [SerializeField] float _fallSafeY = -3f;

        /// 맞으면 이만큼 공격을 못 한다(경직). 넉백 0.16초보다 살짝 길게 잡아
        /// 밀리는 동안 쌓아둔 윈드업이 끝나자마자 터지는 일이 없게 한다.
        [SerializeField] float _hitstunTime = 0.22f;

        /// 공격 예고가 시작될 때 한 번. 지금은 아무도 듣지 않는다 —
        /// 기획에 공격 예고가 있는지 확인되면 연출이 이걸 들으면 된다.
        /// 이음새만 두는 이유: 예고 시간이 0이면 이 이벤트도 안 나가 지금 동작이 그대로다.
        public event Action<MonsterAI, MonsterAttack> Telegraphing;

        public State Current { get; private set; } = State.Idle;

        /// 지금 예고 중인 공격. 없으면 null.
        public MonsterAttack Winding { get; private set; }

        /// 이 몬스터가 서 있는 구역. 구획 밖에 놓였으면 null이고, 그때는 구역 규칙을 적용하지 않는다.
        ///
        /// Start에서 한 번 잡지 않는 이유: 스폰 자리는 같은 오브젝트의 Monster.Start가 세우는데
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

        RegionDefinition _homeRegion;
        bool _homeRegionKnown;

        Monster _monster;
        MonsterDefinition _definition;
        IDamageable _targetDamageable;

        /// 공격마다 따로 센다. 하나로 묶으면 근접을 쓴 뒤 원거리까지 같이 잠긴다.
        float[] _readyAt;

        float _windupEndsAt;
        int _windingIndex = -1;

        public bool IsHitstunned => Time.time < _hitstunUntil;

        float _hitstunUntil;

        void Awake()
        {
            _monster = GetComponent<Monster>();
            if (_body == null) _body = GetComponent<Rigidbody>();
            if (_knockback == null) _knockback = GetComponent<Knockback>();
        }

        void OnEnable() => GetComponent<Monster>().Damaged += OnDamaged;

        void OnDisable() => GetComponent<Monster>().Damaged -= OnDamaged;

        /// 확정 스펙: 넉백을 hitstun으로 승격 — 밀리는 동안 몹의 공격 윈드업을 리셋한다.
        /// 이게 없으면 연타로 녹이는 동안에도 몹이 제 박자대로 때린다.
        void OnDamaged(Monster monster, int amount, GameObject source)
        {
            _hitstunUntil = Time.time + _hitstunTime;
            CancelWindup();
        }

        void Start()
        {
            _definition = _monster.Definition;
            _readyAt = new float[_definition != null ? _definition.Attacks.Count : 0];

            // 플레이어는 씬에 하나뿐이라 시작할 때 한 번만 찾는다.
            if (_target == null)
            {
                var player = FindFirstObjectByType<PlayerHealth>();
                if (player != null) _target = player.transform;
            }

            if (_target != null) _targetDamageable = _target.GetComponent<IDamageable>();
        }

        void FixedUpdate()
        {
            if (_definition == null || !_monster.IsAlive || _target == null)
            {
                Current = State.Idle;
                CancelWindup();
                return;
            }

            // 물리 밀림으로 바닥 아래로 빠졌을 때의 구제. 한 번 빠지면 스스로는 못 돌아온다.
            if (transform.position.y < _fallSafeY)
            {
                CancelWindup();
                _monster.ReturnHome();
                return;
            }

            // 경직 중에는 이동도 공격도 없다. 넉백이 위치를 맡는 구간과 겹친다.
            if (IsHitstunned) return;

            // 맞고 밀리는 동안은 Knockback이 위치를 맡는다. 여기서 같이 MovePosition하면 밀림이 상쇄된다.
            if (_knockback != null && _knockback.IsActive) return;

            Vector3 toTarget = _target.position - transform.position;
            toTarget.y = 0f;
            float distanceToTarget = toTarget.magnitude;

            Vector3 toHome = _monster.HomePosition - transform.position;
            toHome.y = 0f;

            // 예고 중에는 제자리에서 표적만 본다. 쫓아가며 때리면 예고가 피할 시간이 못 된다.
            if (_windingIndex >= 0)
            {
                Current = State.Windup;
                FaceTowards(toTarget);

                if (Time.time < _windupEndsAt) return;

                Fire(_windingIndex, distanceToTarget);
                return;
            }

            // 확장 2-2: 지금은 스폰 지점 기준 거리로 대신한다.
            // 구역(아일랜드) 개념이 들어오면 "플레이어가 구역을 넘었는가"로 바꾸고,
            // 표적 상실 → 복귀는 이 분기를 그대로 쓴다.
            if (toHome.magnitude > _definition.LeashDistance)
            {
                Current = State.Return;
                MoveTowards(toHome);
                return;
            }

            // 차단벽 너머의 플레이어는 표적이 아니다(3차 프로토타입 피드백 1번).
            // 벽은 구역 경계에 서 있으므로 '같은 구역인가'만 물으면 벽 너머가 통째로 걸러진다.
            // 시야 레이캐스트를 쓰지 않은 이유: 광물·구조물 뒤에 선 것까지 놓쳐서
            // 보고된 것보다 넓게 바뀌고, 구역 판정은 그런 오작동이 없다.
            if (distanceToTarget > _definition.DetectRange || !TargetInSameRegion())
            {
                // 표적을 놓치면 스폰 지점으로 돌아간다.
                Current = toHome.sqrMagnitude > 0.05f ? State.Return : State.Idle;
                if (Current == State.Return) MoveTowards(toHome);
                return;
            }

            int choice = PickAttack(distanceToTarget);
            if (choice >= 0)
            {
                Current = State.Attack;
                FaceTowards(toTarget);
                Begin(choice, distanceToTarget);
                return;
            }

            // 사거리 안인데 쓸 것이 없으면(재사용 대기) 제자리에서 기다린다.
            // 더 다가가면 원거리 몬스터가 코앞까지 붙는다.
            if (distanceToTarget <= _definition.MaxAttackRange)
            {
                Current = State.Attack;
                FaceTowards(toTarget);
                return;
            }

            Current = State.Chase;
            FaceTowards(toTarget);
            MoveTowards(toTarget);
        }

        /// 구획되지 않은 땅에 놓인 몬스터는 이 규칙을 적용하지 않는다 —
        /// 구역이 없는데 '같은 구역'을 요구하면 영영 아무도 쫓지 않는다.
        bool TargetInSameRegion()
        {
            RegionDefinition home = HomeRegion;
            if (home == null) return true;
            return RegionMap.At(_target.position) == home;
        }

        /// 쓸 수 있는 것 중 목록에서 앞선 것. 우선순위를 코드가 아니라 에셋 순서가 정한다.
        int PickAttack(float distance)
        {
            if (_targetDamageable == null || !_targetDamageable.IsAlive) return -1;

            var context = new MonsterAttackContext(_monster, _targetDamageable, _target, distance);

            for (int i = 0; i < _definition.Attacks.Count; i++)
            {
                MonsterAttack attack = _definition.Attacks[i];
                if (attack == null) continue;
                if (Time.time < _readyAt[i]) continue;
                if (!attack.CanUse(context)) continue;

                return i;
            }
            return -1;
        }

        /// 예고가 0이면 그 자리에서 바로 내지른다 — 지금 슬라임들이 이 길을 탄다.
        void Begin(int index, float distance)
        {
            MonsterAttack attack = _definition.Attacks[index];
            if (attack.Windup <= 0f)
            {
                Fire(index, distance);
                return;
            }

            _windingIndex = index;
            _windupEndsAt = Time.time + attack.Windup;
            Winding = attack;
            Current = State.Windup;

            Telegraphing?.Invoke(this, attack);
        }

        void Fire(int index, float distance)
        {
            MonsterAttack attack = _definition.Attacks[index];

            // 재사용 대기는 내지른 때부터 센다. 예고 시작부터 세면 예고가 긴 공격이
            // 사실상 더 자주 나간다.
            _readyAt[index] = Time.time + attack.Cooldown;
            CancelWindup();

            attack.Execute(new MonsterAttackContext(_monster, _targetDamageable, _target, distance));
        }

        void CancelWindup()
        {
            _windingIndex = -1;
            Winding = null;
        }

        void MoveTowards(Vector3 direction)
        {
            if (direction.sqrMagnitude < 0.0001f) return;

            Vector3 step = direction.normalized * (_definition.MoveSpeed * Time.fixedDeltaTime);

            // MovePosition: 콜라이더를 밀어내며 이동해 구조물을 관통하지 않는다.
            if (_body != null) _body.MovePosition(_body.position + step);
            else transform.position += step;
        }

        void FaceTowards(Vector3 direction)
        {
            if (direction.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.LookRotation(direction.normalized);
        }

        void OnDrawGizmosSelected()
        {
            MonsterDefinition definition = _definition != null
                ? _definition
                : GetComponent<Monster>() != null ? GetComponent<Monster>().Definition : null;
            if (definition == null) return;

            Gizmos.color = new Color(1f, 0.5f, 0.2f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, definition.DetectRange);

            // 공격마다 사거리가 다르므로 전부 그린다. 하나만 그리면 원거리 몬스터가
            // 어디서부터 쏘는지가 안 보인다.
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.8f);
            for (int i = 0; i < definition.Attacks.Count; i++)
            {
                if (definition.Attacks[i] == null) continue;
                Gizmos.DrawWireSphere(transform.position, definition.Attacks[i].Range);
            }
        }
    }
}
