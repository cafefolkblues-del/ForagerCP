using System;
using UnityEngine;

namespace ForagerCP
{
    /// 몬스터의 판단. 상태: 대기 → 추적 → 예고 → 공격 → 복귀.
    /// 경로 탐색은 아직 직선 추적이다 — 맵에 장애물이 드물어서 NavMesh를 굽는 비용이 더 크다.
    /// 슬라임이 구조물에 끼기 시작하면 NavMeshAgent로 갈아끼우면 되고, 그때 바꿀 곳은 MonsterMotor다.
    ///
    /// 무엇을 내지를지는 여기가 정하지 않는다 — MonsterDefinition이 든 공격 목록에서
    /// 지금 쓸 수 있는 것을 고를 뿐이다. 공격 종류가 늘어도 이 파일은 안 늘어난다.
    ///
    /// 공통 부품 셋을 쓴다: 진행 값(MonsterMemory), 한 걸음 이동(MonsterMotor), 표적·구역 판정(MonsterAggro).
    /// 몹별 상태로 갈라지는 다음 단계는 docs/MONSTER_AI_DESIGN.html 13장.
    ///
    /// ⚠️ 지금 전이 순서는 돌진 → 경직/넉백 → 빈틈 → 예고 → 리쉬 → 탐지·구역 → 후퇴 → 공격 → 추적이고,
    /// 위에 있는 것이 먼저 가로챈다. 건드릴 때 이 순서를 먼저 확인할 것.
    [RequireComponent(typeof(Monster))]
    public class MonsterAI : MonoBehaviour
    {
        public enum State { Idle, Chase, Retreat, Windup, Attack, Recover, Lunge, Return }

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

        /// 돌진을 시작할 때 한 번. 예고와 같은 이유로 연출이 들을 자리다.
        public event Action<MonsterAI> Lunged;

        public State Current { get; private set; } = State.Idle;

        /// 지금 예고 중인 공격. 없으면 null.
        public MonsterAttack Winding => _memory.Winding;

        /// 이 몬스터가 서 있는 구역. 구획 밖에 놓였으면 null. (MonsterAggro.HomeRegion 주석 참고)
        public RegionDefinition HomeRegion => _aggro?.HomeRegion;

        public bool IsLunging => _memory.IsLunging;

        public bool IsHitstunned => Time.time < _memory.HitstunUntil;

        // 필드 초기화로 만드는 이유: 피격 이벤트가 Start보다 먼저 올 수 있다.
        readonly MonsterMemory _memory = new MonsterMemory();
        MonsterMotor _motor;
        MonsterAggro _aggro;

        Monster _monster;
        MonsterDefinition _definition;
        PlayerHealth _player;

        void Awake()
        {
            _monster = GetComponent<Monster>();
            if (_body == null) _body = GetComponent<Rigidbody>();
            if (_knockback == null) _knockback = GetComponent<Knockback>();

            _motor = new MonsterMotor(transform, _body);
            _aggro = new MonsterAggro(_monster);

            // OnEnable이 Start보다 먼저 돌기 때문에 여기서 찾는다.
            _player = FindFirstObjectByType<PlayerHealth>();
        }

        void OnEnable()
        {
            GetComponent<Monster>().Damaged += OnDamaged;

            // 막혔을 때 멈추는 쪽이 자기 자신이라, 플레이어가 몬스터를 찾아 때리는 대신
            // 몬스터가 '내 공격이 막혔나'를 듣는다. 플레이어 쪽이 몬스터를 알 필요가 없다.
            if (_player != null) _player.Blocked += OnBlocked;
        }

        void OnDisable()
        {
            GetComponent<Monster>().Damaged -= OnDamaged;
            if (_player != null) _player.Blocked -= OnBlocked;
        }

        /// 내 공격이 정면 방패에 막혔다. 1구역의 "막으면 잠시 멈추니 반격할 수 있다"가 이것이다.
        void OnBlocked(PlayerHealth health, GameObject source)
        {
            if (source == null || !source.transform.IsChildOf(transform)) return;

            float stagger = _memory.LastFired != null ? _memory.LastFired.StaggerWhenBlocked : _hitstunTime;
            _memory.RecoverUntil = Mathf.Max(_memory.RecoverUntil, Time.time + stagger);
            _memory.CancelWindup();
            _memory.StopLunge();
        }

        /// 확정 스펙: 넉백을 hitstun으로 승격 — 밀리는 동안 몹의 공격 윈드업을 리셋한다.
        /// 이게 없으면 연타로 녹이는 동안에도 몹이 제 박자대로 때린다.
        void OnDamaged(Monster monster, int amount, GameObject source)
        {
            _memory.HitstunUntil = Time.time + _hitstunTime;
            _memory.CancelWindup();
            _memory.StopLunge();
        }

        void Start()
        {
            _definition = _monster.Definition;
            _memory.PrepareCooldowns(_definition != null ? _definition.Attacks.Count : 0);

            // 플레이어는 씬에 하나뿐이라 시작할 때 한 번만 찾는다.
            if (_target == null)
            {
                var player = FindFirstObjectByType<PlayerHealth>();
                if (player != null) _target = player.transform;
            }

            _aggro.Acquire(_target);
        }

        void FixedUpdate()
        {
            if (_definition == null || !_monster.IsAlive || !_aggro.HasTarget)
            {
                Current = State.Idle;
                _memory.CancelWindup();
                return;
            }

            // 물리 밀림으로 바닥 아래로 빠졌을 때의 구제. 한 번 빠지면 스스로는 못 돌아온다.
            if (transform.position.y < _fallSafeY)
            {
                _memory.CancelWindup();
                _monster.ReturnHome();
                return;
            }

            // 돌진은 위치를 직접 쥐는 구간이라 다른 판단보다 먼저 처리한다.
            if (_memory.IsLunging)
            {
                Current = State.Lunge;
                TickLunge();
                return;
            }

            // 경직 중에는 이동도 공격도 없다. 넉백이 위치를 맡는 구간과 겹친다.
            if (IsHitstunned) return;

            // 맞고 밀리는 동안은 Knockback이 위치를 맡는다. 여기서 같이 MovePosition하면 밀림이 상쇄된다.
            if (_knockback != null && _knockback.IsActive) return;

            Vector3 toTarget = _aggro.ToTarget(transform.position);
            float distanceToTarget = toTarget.magnitude;
            Vector3 toHome = _aggro.ToHome(transform.position);

            // 내지른 뒤의 빈틈. 이동도 다음 공격도 없다 — 이 구간이 플레이어의 반격 창이다.
            if (Time.time < _memory.RecoverUntil)
            {
                Current = State.Recover;
                _motor.Face(toTarget);
                return;
            }

            // 예고 중에는 제자리에서 표적만 본다. 쫓아가며 때리면 예고가 피할 시간이 못 된다.
            if (_memory.IsWindingUp)
            {
                Current = State.Windup;
                _motor.Face(toTarget);

                if (Time.time < _memory.WindupEndsAt) return;

                Fire(_memory.WindingIndex, distanceToTarget);
                return;
            }

            // 표적 상실 → 복귀는 구역 경계 판정이 들어와도 이 분기를 그대로 쓴다.
            if (_aggro.PastLeash(toHome, _definition.LeashDistance))
            {
                Current = State.Return;
                _motor.Step(toHome, _definition.MoveSpeed);
                return;
            }

            // 차단벽 너머의 플레이어는 표적이 아니다(3차 프로토타입 피드백 1번).
            if (_aggro.IsLost(distanceToTarget, _definition.DetectRange))
            {
                // 표적을 놓치면 스폰 지점으로 돌아간다.
                Current = toHome.sqrMagnitude > 0.05f ? State.Return : State.Idle;
                if (Current == State.Return) _motor.Step(toHome, _definition.MoveSpeed);
                return;
            }

            // 원거리형은 붙으면 물러난다. 물러날 거리는 공격이 들고 있는 최소 사거리에서 나온다 —
            // 따로 수치를 두면 "안 쏘는 거리"와 "물러나는 거리"가 어긋난다.
            // 기획이 "구역 밖으로 이동하지 않는다"로 못박아서, 구역을 벗어나는 걸음이면 물러나지 않는다.
            if (distanceToTarget < _definition.RetreatRange
                && _motor.TryStepWithin(-toTarget, _definition.MoveSpeed, _aggro.HomeRegion))
            {
                Current = State.Retreat;
                _motor.Face(toTarget);
                return;
            }

            int choice = PickAttack(distanceToTarget);
            if (choice >= 0)
            {
                Current = State.Attack;
                _motor.Face(toTarget);
                Begin(choice, distanceToTarget);
                return;
            }

            // 사거리 안인데 쓸 것이 없으면(재사용 대기) 제자리에서 기다린다.
            // 더 다가가면 원거리 몬스터가 코앞까지 붙는다.
            if (distanceToTarget <= _definition.MaxAttackRange)
            {
                Current = State.Attack;
                _motor.Face(toTarget);
                return;
            }

            Current = State.Chase;
            _motor.Face(toTarget);
            _motor.Step(toTarget, _definition.MoveSpeed);
        }

        /// 쓸 수 있는 것 중 목록에서 앞선 것. 우선순위를 코드가 아니라 에셋 순서가 정한다.
        int PickAttack(float distance)
        {
            IDamageable target = _aggro.TargetDamageable;
            if (target == null || !target.IsAlive) return -1;

            var context = new MonsterAttackContext(_monster, this, target, _aggro.Target, distance);

            for (int i = 0; i < _definition.Attacks.Count; i++)
            {
                MonsterAttack attack = _definition.Attacks[i];
                if (attack == null) continue;
                if (Time.time < _memory.ReadyAt[i]) continue;
                if (!attack.CanUse(context)) continue;

                return i;
            }
            return -1;
        }

        /// 예고가 0이면 그 자리에서 바로 내지른다.
        void Begin(int index, float distance)
        {
            MonsterAttack attack = _definition.Attacks[index];
            if (attack.Windup <= 0f)
            {
                Fire(index, distance);
                return;
            }

            _memory.WindingIndex = index;
            _memory.WindupEndsAt = Time.time + attack.Windup;
            _memory.Winding = attack;
            Current = State.Windup;

            Telegraphing?.Invoke(this, attack);
        }

        void Fire(int index, float distance)
        {
            MonsterAttack attack = _definition.Attacks[index];

            // 재사용 대기는 내지른 때부터 센다. 예고 시작부터 세면 예고가 긴 공격이
            // 사실상 더 자주 나간다.
            _memory.ReadyAt[index] = Time.time + attack.Cooldown;
            _memory.RecoverUntil = Time.time + attack.Recovery;
            _memory.LastFired = attack;
            _memory.CancelWindup();

            attack.Execute(new MonsterAttackContext(_monster, this, _aggro.TargetDamageable, _aggro.Target, distance));
        }

        /// 돌진을 시작한다. 공격 에셋이 부탁하고 이동은 여기가 맡는다.
        public void BeginLunge(Vector3 direction, float speed, float distance,
                               int damage, DamageKind kind, float hitRadius)
        {
            if (direction.sqrMagnitude < 0.0001f || distance <= 0f) return;

            _memory.LungeDirection = direction.normalized;
            _memory.LungeSpeed = Mathf.Max(0.1f, speed);
            _memory.LungeRemaining = distance;
            _memory.LungeDamage = damage;
            _memory.LungeKind = kind;
            _memory.LungeHitRadius = Mathf.Max(0.1f, hitRadius);
            _memory.LungeHasHit = false;

            _motor.Face(_memory.LungeDirection);
            Lunged?.Invoke(this);
        }

        /// 한 번만 맞히고, 벽에 닿거나 거리를 다 쓰면 멈춘다.
        /// 멈춘 뒤의 빈틈은 Fire가 이미 걸어두었다(돌진 공격의 Recovery).
        void TickLunge()
        {
            MonsterMemory m = _memory;
            float step = Mathf.Min(m.LungeRemaining, m.LungeSpeed * Time.fixedDeltaTime);
            Vector3 from = _motor.Position;

            IDamageable target = _aggro.TargetDamageable;
            if (!m.LungeHasHit && target != null && target.IsAlive)
            {
                Vector3 flat = _aggro.Target.position - from;
                flat.y = 0f;

                if (flat.magnitude <= m.LungeHitRadius)
                {
                    target.TakeDamage(m.LungeDamage, gameObject, m.LungeKind);
                    m.LungeHasHit = true;
                }
            }

            // 벽을 만나면 그 자리에서 끝난다. 뚫고 지나가면 구역 밖으로 나가버린다.
            if (Physics.SphereCast(from + Vector3.up * 0.5f, m.LungeHitRadius * 0.8f,
                    m.LungeDirection, out RaycastHit hit, step, ~0, QueryTriggerInteraction.Ignore)
                && hit.collider.GetComponentInParent<IDamageable>() == null)
            {
                m.StopLunge();
                return;
            }

            _motor.MoveTo(from + m.LungeDirection * step);
            m.LungeRemaining -= step;
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
