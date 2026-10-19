using System;
using System.Collections.Generic;
using UnityEngine;

namespace ForagerCP
{
    /// 좌클릭 하나로 채집과 전투를 겸한다.
    ///
    /// 확정 스펙:
    /// - 검은 선딜 → 액티브 → 후딜의 세 구간을 가지며, 액티브 동안에만 판정이 산다.
    /// - 기본 평타는 부채꼴 안의 '가장 가까운 하나'만 때린다. 다중타격은 스킬로 해금한다.
    /// - 스윙을 시작할 때 사거리 안 최근접 적 쪽으로 한 번 돌아본다(자동 방향 보정).
    /// - 좌클릭을 누르고 있으면 차지가 차고, 떼는 순간 발동한다.
    ///   완충이면 원형 360° 광역, 덜 찼으면 일반 스윙.
    /// - 정면에 광물이 있으면 차지를 누르고 채집으로 분기한다(캐다가 원치 않는 차지가 걸리지 않게).
    /// - 맞으면(넉백) 차지와 스윙이 취소된다 — PlayerActionState가 CanAttack=false로 알려준다.
    public class PlayerAttacker : MonoBehaviour
    {
        public enum Phase { Idle, Windup, Active, Recovery }

        [SerializeField] GameInput _input;
        [SerializeField] PlayerActionState _state;
        [SerializeField] HarvestPower _power;
        [SerializeField] PlayerEquipment _equipment;
        [SerializeField] PlayerBuffs _buffs;
        [SerializeField] ProgressFlags _flags;

        [Header("검 판정")]
        [SerializeField] float _range = 2.5f;
        [SerializeField, Range(0f, 180f)] float _halfAngle = 45f;

        [Header("스윙 구간 (초)")]
        [SerializeField] float _windupTime = 0.10f;
        [SerializeField] float _activeTime = 0.12f;
        [SerializeField] float _recoveryTime = 0.18f;

        [Header("자동 방향 보정")]
        /// 커서가 가리키는 쪽에서 이 각도 안에 있는 적만 겨냥한다(전체 각). 360이면 전방위.
        /// 뒤에 선 적까지 집으면 칼이 홱 돌아가 조작이 손에서 떠난다.
        [SerializeField, Range(0f, 360f)] float _aimConeDegrees = 120f;

        [Header("다중타격")]
        /// 이 기록이 서 있으면 부채꼴 안을 전부 때린다. 스킬이 세운다(FlagSkillEffect).
        /// 문자열을 코드에 박지 않는 이유는 기획이 스킬 구성을 바꿔도 코드가 안 따라가게 하려는 것.
        [SerializeField] string _multiHitFlag = "attack_multi";

        [Header("차지")]
        [SerializeField] float _chargeTime = 0.8f;
        [SerializeField] float _chargeRange = 3.0f;
        [SerializeField] float _chargeDamageMultiplier = 1.5f;

        [Header("채집")]
        [SerializeField] float _harvestInterval = 0.4f;

        /// 채집과 공격이 후딜을 함께 쓴다. 끄면 예전처럼 따로 센다.
        ///
        /// 따로 세면 캐다가 바로 칼을 휘두르고 또 바로 캐는 식으로 두 박자가 겹쳐,
        /// 한쪽 박자만 보고 잡은 밸런스가 전부 무너진다(2차 프로토타입 피드백 1번).
        [SerializeField] bool _shareActionCooldown = true;

        public event Action<bool> SwingStarted; // true = 차지 스윙
        public event Action<float> ChargeRatioChanged;

        public Phase Current { get; private set; } = Phase.Idle;

        /// 차지가 차고 있는 중인지. 이동 배율을 깎는 근거라 PlayerActionState가 읽는다.
        public bool IsCharging { get; private set; }

        /// 스윙이 조준을 쥐고 있는 동안 참. PlayerMotor가 커서 조준에서 손을 떼는 근거다.
        public bool HasAimLock { get; private set; }

        /// 다중타격이 열렸는지. 차지는 이것과 무관하게 언제나 광역이다(확정 예외).
        public bool MultiHitUnlocked => _flags != null
            && !string.IsNullOrEmpty(_multiHitFlag)
            && _flags.Has(_multiHitFlag);

        public float ChargeRatio => _chargeTime <= 0f ? 1f : Mathf.Clamp01(_input.AttackHoldTime / _chargeTime);

        /// 한 스윙에서 이미 때린 대상. 액티브가 여러 프레임이라 중복 타격을 막아야 한다.
        readonly HashSet<IDamageable> _hitThisSwing = new HashSet<IDamageable>();
        readonly Collider[] _overlapBuffer = new Collider[32];

        float _phaseEndTime;
        bool _swingIsCharged;
        bool _harvestLock;
        float _nextHarvestTime;

        /// 채집이든 공격이든 '다음 행동'이 가능해지는 시각. 둘이 같은 값을 민다.
        float _nextActionTime;

        void Awake()
        {
            if (_input == null) _input = GetComponent<GameInput>();
            if (_state == null) _state = GetComponent<PlayerActionState>();
            if (_power == null) _power = GetComponent<HarvestPower>();
            if (_equipment == null) _equipment = FindFirstObjectByType<PlayerEquipment>();
            if (_buffs == null) _buffs = FindFirstObjectByType<PlayerBuffs>();
            if (_flags == null) _flags = FindFirstObjectByType<ProgressFlags>();
        }

        void OnEnable() => _input.AttackReleased += OnAttackReleased;

        void OnDisable() => _input.AttackReleased -= OnAttackReleased;

        void Update()
        {
            // 맞았거나 방패를 들면 진행 중인 것도 전부 취소된다(피격 캔슬).
            if (_state != null && !_state.CanAttack)
            {
                CancelSwing();
                return;
            }

            TickSwing();

            if (Current != Phase.Idle) return;
            TickHoldAndHarvest();
        }

        // ---------------- 홀드: 채집이냐 차지냐 ----------------

        void TickHoldAndHarvest()
        {
            if (!_input.AttackHeld)
            {
                _harvestLock = false;
                SetCharging(false);
                return;
            }

            // 홀드를 시작한 프레임에 정면 대상을 보고 모드를 잠근다.
            // 캐는 도중에 광물이 깨져도 모드가 흔들리지 않게 유지한다.
            if (!_harvestLock && !IsCharging)
            {
                if (FindNearestHarvestable(_range) != null) _harvestLock = true;
                else SetCharging(true);
            }

            if (_harvestLock)
            {
                TickHarvest();
                return;
            }

            ChargeRatioChanged?.Invoke(ChargeRatio);
        }

        /// 버프가 줄여준 만큼 짧아진 채집 간격. 비율은 더해서 한 번만 먹인다.
        float HarvestInterval
        {
            get
            {
                int percent = _power != null ? _power.HarvestSpeedPercent : 0;

                // 90%까지만 깎는다. 100%가 되면 간격이 0이 되어 한 프레임에 광물이 사라진다.
                float scale = 1f - Mathf.Clamp(percent, 0, 90) / 100f;
                return Mathf.Max(0.05f, _harvestInterval * scale);
            }
        }

        void TickHarvest()
        {
            if (Time.time < _nextHarvestTime) return;
            if (_shareActionCooldown && Time.time < _nextActionTime) return;

            IHarvestable target = FindNearestHarvestable(_range);
            if (target == null) return;

            _nextHarvestTime = Time.time + HarvestInterval;
            _nextActionTime = _nextHarvestTime;

            target.Harvest(_power != null ? _power.Value : 1);
        }

        void OnAttackReleased(float holdTime)
        {
            // 채집 중이었으면 스윙이 나가지 않는다.
            if (_harvestLock)
            {
                _harvestLock = false;
                return;
            }

            if (_state != null && !_state.CanAttack) { SetCharging(false); return; }
            if (Current != Phase.Idle) { SetCharging(false); return; }

            // 캐던 손으로 바로 베지 못한다. 채집이 민 후딜이 아직 남아 있으면 스윙도 안 나간다.
            if (_shareActionCooldown && Time.time < _nextActionTime) { SetCharging(false); return; }

            bool charged = _chargeTime > 0f && holdTime >= _chargeTime;
            SetCharging(false);
            StartSwing(charged);
        }

        void SetCharging(bool charging)
        {
            if (IsCharging == charging) return;

            IsCharging = charging;
            if (!charging) ChargeRatioChanged?.Invoke(0f);
        }

        // ---------------- 스윙 ----------------

        void StartSwing(bool charged)
        {
            _swingIsCharged = charged;
            _hitThisSwing.Clear();

            // 겨눌 상대를 못 찾으면 아래에서 손대지 않으므로 여기서 먼저 내려둔다.
            // 안 그러면 '직전 스윙이 켜둔 잠금'이 남아 커서 조준이 영영 안 돌아온다.
            HasAimLock = false;

            // 차지는 어차피 360°라 겨눌 이유가 없다.
            if (!charged) AimAtNearestEnemy();

            Current = Phase.Windup;
            _phaseEndTime = Time.time + _windupTime;
            SwingStarted?.Invoke(charged);
        }

        void TickSwing()
        {
            switch (Current)
            {
                case Phase.Idle:
                    return;

                case Phase.Windup:
                    if (Time.time < _phaseEndTime) return;
                    Current = Phase.Active;
                    _phaseEndTime = Time.time + _activeTime;
                    return;

                case Phase.Active:
                    // 액티브가 여러 프레임이라 매 프레임 훑는다. 중복은 _hitThisSwing이 막는다.
                    ApplyHits();
                    if (Time.time < _phaseEndTime) return;
                    Current = Phase.Recovery;
                    _phaseEndTime = Time.time + _recoveryTime;
                    return;

                case Phase.Recovery:
                    if (Time.time < _phaseEndTime) return;
                    Current = Phase.Idle;
                    HasAimLock = false;

                    // 벤 손으로 바로 캐지도 못한다. 반대 방향도 같이 막아야 박자가 하나가 된다.
                    _nextActionTime = Time.time + HarvestInterval;
                    return;
            }
        }

        void CancelSwing()
        {
            Current = Phase.Idle;
            _hitThisSwing.Clear();
            _harvestLock = false;
            HasAimLock = false;
            SetCharging(false);
        }

        /// 스윙을 시작할 때 한 번만 겨눈다.
        ///
        /// 액티브 동안 계속 다시 겨누면 적을 따라 칼이 휘는 유도탄이 된다.
        /// 스펙은 '방향 보정'이지 추적이 아니다.
        void AimAtNearestEnemy()
        {
            IDamageable target = FindNearestEnemy(_range, _aimConeDegrees * 0.5f);
            if (target == null) return;

            Vector3 look = target.Transform.position - transform.position;
            look.y = 0f;
            if (look.sqrMagnitude < 0.0001f) return;

            // 겨눌 때는 8방향 스냅을 쓰지 않는다. 스냅하면 보정한 각이 도로 어긋난다.
            transform.rotation = Quaternion.LookRotation(look);
            HasAimLock = true;
        }

        /// 광역이냐 단일이냐. 차지는 확정 예외라 해금과 무관하게 언제나 광역이다.
        bool HitsEveryone => _swingIsCharged || MultiHitUnlocked;

        void ApplyHits()
        {
            float range = _swingIsCharged ? _chargeRange : _range;
            float halfAngle = _swingIsCharged ? 180f : _halfAngle; // 차지는 원형 360°
            // 공격력은 장비 한 곳에서만 나온다. 여기 숫자를 박으면 무기 티어를 늘릴 때 코드를 고쳐야 한다.
            //
            // 버프는 더 이상 숫자를 더하지 않고 비율을 올린다. 기획이 "기본 능력치에 각각의
            // 증가율을 더한 뒤 한 번 계산"으로 못박았으므로, 비율끼리 먼저 더하고 곱은 한 번이다.
            int weapon = Mathf.Max(1, _equipment != null ? _equipment.AttackPower : 1);
            float bonus = 1f + (_buffs != null ? _buffs.AttackPower : 0) / 100f;

            int damage = Mathf.RoundToInt(weapon * bonus
                                          * (_swingIsCharged ? _chargeDamageMultiplier : 1f));

            if (!HitsEveryone)
            {
                // 한 스윙에 한 명. 액티브가 여러 프레임이라, '아직 안 때린 것 중 최근접'을 매 프레임
                // 고르면 한 번 휘둘러 여러 명을 차례로 베게 된다 — 단일 타격이 이름뿐이 된다.
                if (_hitThisSwing.Count > 0) return;

                IDamageable single = FindNearestEnemy(range, halfAngle);
                if (single == null) return;

                _hitThisSwing.Add(single);
                single.TakeDamage(damage, gameObject);
                return;
            }

            int count = Physics.OverlapSphereNonAlloc(transform.position, range, _overlapBuffer);
            Vector3 facing = transform.forward;

            for (int i = 0; i < count; i++)
            {
                var damageable = _overlapBuffer[i].GetComponentInParent<IDamageable>();
                if (damageable == null || !damageable.IsAlive) continue;
                if (damageable.Transform == transform) continue;      // 자기 자신 제외
                if (_hitThisSwing.Contains(damageable)) continue;     // 스윙당 1회

                Vector3 toTarget = damageable.Transform.position - transform.position;
                toTarget.y = 0f;
                if (halfAngle < 180f && Vector3.Angle(facing, toTarget) > halfAngle) continue;

                _hitThisSwing.Add(damageable);
                damageable.TakeDamage(damage, gameObject);
            }
        }

        /// 겨눌 때와 때릴 때가 같은 규칙을 써야 "돌아봤는데 안 맞는다"가 안 생긴다.
        /// halfAngle은 반각(45면 부채꼴 90도).
        IDamageable FindNearestEnemy(float range, float halfAngle)
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, range, _overlapBuffer);
            Vector3 facing = transform.forward;

            IDamageable best = null;
            float bestSqrDistance = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                var damageable = _overlapBuffer[i].GetComponentInParent<IDamageable>();
                if (damageable == null || !damageable.IsAlive) continue;
                if (damageable.Transform == transform) continue;
                if (_hitThisSwing.Contains(damageable)) continue;

                Vector3 toTarget = damageable.Transform.position - transform.position;
                toTarget.y = 0f;
                if (halfAngle < 180f && Vector3.Angle(facing, toTarget) > halfAngle) continue;

                float sqrDistance = toTarget.sqrMagnitude;
                if (sqrDistance >= bestSqrDistance) continue;

                bestSqrDistance = sqrDistance;
                best = damageable;
            }

            return best;
        }

        // ---------------- 대상 탐색 ----------------

        /// 채집은 종전대로 최근접 하나만 캔다(다단히트는 검 판정 한정).
        IHarvestable FindNearestHarvestable(float range)
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, range, _overlapBuffer);

            IHarvestable best = null;
            float bestSqrDistance = float.MaxValue;
            Vector3 facing = transform.forward;

            for (int i = 0; i < count; i++)
            {
                var candidate = _overlapBuffer[i].GetComponentInParent<IHarvestable>();
                if (candidate == null || !candidate.IsHarvestable) continue;

                // 잠긴 구역의 광물은 벽 너머로도 안 캐진다. 안 막으면 개방 비용을
                // 치르지 않고 벽에 붙어 캐게 된다(2차 프로토타입 피드백 2번).
                if (!RegionGate.Allows(candidate.Transform)) continue;

                Vector3 toTarget = candidate.Transform.position - transform.position;
                toTarget.y = 0f;
                if (Vector3.Angle(facing, toTarget) > _halfAngle) continue;

                float sqrDistance = toTarget.sqrMagnitude;
                if (sqrDistance >= bestSqrDistance) continue;

                bestSqrDistance = sqrDistance;
                best = candidate;
            }

            return best;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _range);

            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, _chargeRange);
        }
    }
}
