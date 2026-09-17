using UnityEngine;

namespace ForagerCP
{
    /// 몬스터의 이동·공격. 상태: 대기 → 추적 → 공격 → 복귀.
    /// 경로 탐색은 아직 직선 추적이다 — 맵에 장애물이 드물어서 NavMesh를 굽는 비용이 더 크다.
    /// 슬라임이 구조물에 끼기 시작하면 NavMeshAgent로 갈아끼우면 되고, 그때 바꿀 곳은 MoveTowards 하나다.
    [RequireComponent(typeof(Monster))]
    public class MonsterAI : MonoBehaviour
    {
        public enum State { Idle, Chase, Attack, Return }

        [SerializeField] Transform _target;
        [SerializeField] Rigidbody _body;
        [SerializeField] Knockback _knockback;

        public State Current { get; private set; } = State.Idle;

        Monster _monster;
        MonsterDefinition _definition;
        IDamageable _targetDamageable;
        float _nextAttackTime;

        void Awake()
        {
            _monster = GetComponent<Monster>();
            if (_body == null) _body = GetComponent<Rigidbody>();
            if (_knockback == null) _knockback = GetComponent<Knockback>();
        }

        void Start()
        {
            _definition = _monster.Definition;

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
                return;
            }

            // 맞고 밀리는 동안은 Knockback이 위치를 맡는다. 여기서 같이 MovePosition하면 밀림이 상쇄된다.
            if (_knockback != null && _knockback.IsActive) return;

            Vector3 toTarget = _target.position - transform.position;
            toTarget.y = 0f;
            float distanceToTarget = toTarget.magnitude;

            Vector3 toHome = _monster.HomePosition - transform.position;
            toHome.y = 0f;

            // 확장 2-2: 지금은 스폰 지점 기준 거리로 대신한다.
            // 구역(아일랜드) 개념이 들어오면 "플레이어가 구역을 넘었는가"로 바꾸고,
            // 표적 상실 → 복귀는 이 분기를 그대로 쓴다.
            if (toHome.magnitude > _definition.LeashDistance)
            {
                Current = State.Return;
                MoveTowards(toHome);
                return;
            }

            if (distanceToTarget > _definition.DetectRange)
            {
                // 표적을 놓치면 스폰 지점으로 돌아간다.
                Current = toHome.sqrMagnitude > 0.05f ? State.Return : State.Idle;
                if (Current == State.Return) MoveTowards(toHome);
                return;
            }

            if (distanceToTarget <= _definition.AttackRange)
            {
                Current = State.Attack;
                FaceTowards(toTarget);
                TryAttack();
                return;
            }

            Current = State.Chase;
            FaceTowards(toTarget);
            MoveTowards(toTarget);
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

        void TryAttack()
        {
            if (Time.time < _nextAttackTime) return;
            if (_targetDamageable == null || !_targetDamageable.IsAlive) return;

            _nextAttackTime = Time.time + _definition.AttackInterval;
            _targetDamageable.TakeDamage(_definition.AttackPower, gameObject);
        }

        void OnDrawGizmosSelected()
        {
            MonsterDefinition definition = _definition != null
                ? _definition
                : GetComponent<Monster>() != null ? GetComponent<Monster>().Definition : null;
            if (definition == null) return;

            Gizmos.color = new Color(1f, 0.5f, 0.2f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, definition.DetectRange);

            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, definition.AttackRange);
        }
    }
}
