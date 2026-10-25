using System;
using UnityEngine;

namespace ForagerCP
{
    /// 몬스터 상태머신의 실행기. 모든 몹이 같고, 몹별로 바꿀 수 없다.
    ///
    /// 매 물리 프레임 다섯 단계를 돈다 (docs/MONSTER_AI_DESIGN.html 4장):
    ///   1. 가드 — 죽음·표적 없음 → Idle, 맵 아래로 빠짐 → 집으로
    ///   2. 방해 처리 — 몸을 쓰는 공격 중이면 AttackMotion, 경직·넉백 중이면 Stagger, 막혀서 빈틈이 늘었으면 Recover
    ///   3. 어그로 — 표적을 잃으면 Return, 다시 보이면 Chase (예고·빈틈처럼 Committed인 칸에서는 미룬다)
    ///   4. 지금 칸의 상태 실행 — 상태가 다음 칸 이름을 돌려준다
    ///   5. 전환 — 칸이 바뀌면 Exit → Enter → StateChanged, 바뀐 칸을 같은 프레임에 이어서 실행
    ///
    /// 몹마다 다른 행동은 4단계에서만 일어난다. 2·3단계를 상태에 맡기지 않는 이유:
    /// 상태마다 "맞았나? 벽 너머인가?"를 각자 물으면 하나만 빠뜨려도 이미 고친 버그가 그 몹에서만 되살아난다.
    /// (플레이어 쪽 PlayerActionState가 같은 이유로 우선순위를 한 곳에서 중재한다.)
    ///
    /// 칸에 어떤 구현이 들어갈지는 MonsterDefinition.Pattern이 정한다. 공통 칸(Idle·Return·Stagger·AttackMotion)은
    /// 여기 들어 있다. 경로 탐색은 아직 직선 추적이다 — 바꿀 곳은 MonsterMotor다.
    [RequireComponent(typeof(Monster))]
    public class MonsterAI : MonoBehaviour
    {
        [SerializeField] Transform _target;
        [SerializeField] Rigidbody _body;
        [SerializeField] Knockback _knockback;

        /// 이 높이 아래로 내려가면 맵을 뚫은 것으로 본다.
        [SerializeField] float _fallSafeY = -3f;

        /// 공격 예고가 시작될 때 한 번(WindupState가 부른다). 지금은 아무도 듣지 않는다 —
        /// 기획에 공격 예고가 있는지 확인되면 연출이 이걸 들으면 된다.
        public event Action<MonsterAI, MonsterAttack> Telegraphing;

        /// 칸이 바뀔 때마다 (실행기, 이전 칸, 새 칸). 애니메이션·사운드·디버그 표시가 듣는다.
        public event Action<MonsterAI, StateId, StateId> StateChanged;

        public StateId Current { get; private set; } = StateId.Idle;

        /// 지금 예고 중인 공격. 없으면 null.
        public MonsterAttack Winding => _memory.Winding;

        /// 이 몬스터가 서 있는 구역. 구획 밖에 놓였으면 null. (MonsterAggro.HomeRegion 주석 참고)
        public RegionDefinition HomeRegion => _aggro?.HomeRegion;

        public bool IsHitstunned => Time.time < _memory.HitstunUntil;

        /// 한 프레임에 칸을 이어서 실행하는 최대 횟수. 칸끼리 서로 넘기는 실수가 있어도 프레임이 멈추지 않게.
        const int MaxStepsPerFrame = 4;

        // 필드 초기화로 만드는 이유: 피격 이벤트가 Start보다 먼저 올 수 있다.
        readonly MonsterMemory _memory = new MonsterMemory();
        MonsterMotor _motor;
        MonsterAggro _aggro;
        MonsterContext _ctx;

        Monster _monster;
        MonsterDefinition _definition;
        MonsterPattern _pattern;
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
        /// 빈틈을 늘려 두면 다음 프레임 방해 처리가 Recover로 보낸다.
        void OnBlocked(PlayerHealth health, GameObject source)
        {
            if (source == null || !source.transform.IsChildOf(transform)) return;

            float stagger = _memory.LastFired != null ? _memory.LastFired.StaggerWhenBlocked : HitstunTime;
            _memory.RecoverUntil = Mathf.Max(_memory.RecoverUntil, Time.time + stagger);
            _memory.CancelWindup();
            _memory.StopMotion();
        }

        /// 경직 시간은 종류마다 MonsterDefinition에 있다. 피격은 Start보다 먼저 올 수 있어 Monster에서 바로 읽는다.
        float HitstunTime => _monster != null && _monster.Definition != null ? _monster.Definition.HitstunTime : 0f;

        /// 확정 스펙: 넉백을 hitstun으로 승격 — 밀리는 동안 몹의 공격 윈드업을 리셋한다.
        /// 이게 없으면 연타로 녹이는 동안에도 몹이 제 박자대로 때린다.
        void OnDamaged(Monster monster, int amount, GameObject source)
        {
            _memory.HitstunUntil = Time.time + HitstunTime;
            _memory.CancelWindup();
            _memory.StopMotion();
        }

        void Start()
        {
            _definition = _monster.Definition;
            _pattern = _definition != null ? _definition.Pattern : null;
            _memory.PrepareCooldowns(_definition != null ? _definition.Attacks.Count : 0);

            // 플레이어는 씬에 하나뿐이라 시작할 때 한 번만 찾는다.
            if (_target == null)
            {
                var player = FindFirstObjectByType<PlayerHealth>();
                if (player != null) _target = player.transform;
            }

            _aggro.Acquire(_target);
            _ctx = new MonsterContext(this, _monster, _definition, _memory, _motor, _aggro);
        }

        void FixedUpdate()
        {
            // ---- 1. 가드 ----
            if (_definition == null || !_monster.IsAlive || !_aggro.HasTarget)
            {
                ChangeTo(StateId.Idle);
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

            // ---- 2. 방해 처리 ----
            // 몸을 쓰는 공격은 위치를 직접 쥐는 구간이라 경직보다도 먼저 본다(맞으면 끊기므로 둘이 겹치지는 않는다).
            if (_memory.InMotion)
            {
                ChangeTo(StateId.AttackMotion);
            }
            // 경직·넉백 중에는 이동도 공격도 없다. 밀리는 동안은 Knockback이 위치를 맡는다 —
            // 여기서 같이 MovePosition하면 밀림이 상쇄된다.
            else if (IsHitstunned || (_knockback != null && _knockback.IsActive))
            {
                ChangeTo(StateId.Stagger);
                return;
            }
            else if (Current == StateId.Stagger)
            {
                ChangeTo(Time.time < _memory.RecoverUntil ? StateId.Recover : StateId.Chase);
            }
            // 방패에 막혀 빈틈이 늘었다. 예고 중이었어도 빈틈이 이긴다.
            else if (Time.time < _memory.RecoverUntil && Current != StateId.Recover)
            {
                ChangeTo(StateId.Recover);
            }

            _ctx.Measure(transform.position);

            // ---- 3~5. 어그로 → 상태 실행 → 전환 ----
            for (int i = 0; i < MaxStepsPerFrame; i++)
            {
                if (!IsCommitted(Current)) ChangeTo(DecideByAggro());

                StateId next = TickCurrent();
                if (next == Current) break;
                ChangeTo(next);
            }
        }

        /// 표적을 잃었으면 복귀(집이면 대기), 다시 보이면 추적. 그 밖에는 지금 칸 그대로.
        StateId DecideByAggro()
        {
            bool lost = _aggro.PastLeash(_ctx.ToHome, _definition.LeashDistance)
                        || _aggro.IsLost(_ctx.Distance, _definition.DetectRange);

            if (lost) return _ctx.ToHome.sqrMagnitude > 0.05f ? StateId.Return : StateId.Idle;
            if (Current == StateId.Idle || Current == StateId.Return) return StateId.Chase;
            return Current;
        }

        StateId TickCurrent()
        {
            switch (Current)
            {
                case StateId.Idle:
                    return StateId.Idle;

                case StateId.Return:
                    if (_ctx.ToHome.sqrMagnitude <= 0.05f) return StateId.Idle;
                    _motor.Step(_ctx.ToHome, _definition.MoveSpeed);
                    return StateId.Return;

                case StateId.Stagger:
                    return StateId.Stagger;

                case StateId.AttackMotion:
                    if (_memory.InMotion && _memory.Motion.TickMotion(_ctx)) return StateId.AttackMotion;
                    _memory.StopMotion();
                    return _ctx.AfterFire();

                default:
                    MonsterState state = Resolve(Current);
                    return state != null ? state.Tick(_ctx) : StateId.Chase;
            }
        }

        bool IsCommitted(StateId id)
        {
            if (id == StateId.AttackMotion || id == StateId.Stagger) return true;
            MonsterState state = Resolve(id);
            return state != null && state.Committed;
        }

        void ChangeTo(StateId next)
        {
            if (next == Current) return;

            StateId previous = Current;
            if (_ctx != null) Resolve(previous)?.Exit(_ctx);
            Current = next;
            if (_ctx != null) Resolve(next)?.Enter(_ctx);

            StateChanged?.Invoke(this, previous, next);
        }

        /// 칸 이름 → 이 몹의 구현. 공통 칸은 null.
        public MonsterState Resolve(StateId id) => MonsterPattern.Resolve(_pattern, id);

        /// WindupState.Enter가 부른다. 이벤트는 선언한 클래스 밖에서 직접 쏠 수 없어 통로를 둔다.
        public void RaiseTelegraphing(MonsterAttack attack) => Telegraphing?.Invoke(this, attack);

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
