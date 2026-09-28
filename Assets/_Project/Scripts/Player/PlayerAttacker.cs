using System;
using System.Collections.Generic;
using UnityEngine;

namespace ForagerCP
{
    /// 좌클릭 하나로 채집과 전투를 겸한다.
    ///
    /// 확정 스펙:
    /// - 검은 선딜 → 액티브 → 후딜의 세 구간을 가지며, 액티브 동안에만 판정이 산다.
    /// - 액티브 동안 부채꼴 안의 몬스터를 전부 때린다(다단히트). 같은 대상은 한 스윙에 한 번만.
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
        [SerializeField] PlayerWeapon _weapon;

        [Header("검 판정")]
        [SerializeField] float _range = 2.5f;
        [SerializeField, Range(0f, 180f)] float _halfAngle = 45f;

        [Header("스윙 구간 (초)")]
        [SerializeField] float _windupTime = 0.10f;
        [SerializeField] float _activeTime = 0.12f;
        [SerializeField] float _recoveryTime = 0.18f;

        [Header("차지")]
        [SerializeField] float _chargeTime = 0.8f;
        [SerializeField] float _chargeRange = 3.0f;
        [SerializeField] float _chargeDamageMultiplier = 1.5f;

        [Header("채집")]
        [SerializeField] float _harvestInterval = 0.4f;

        public event Action<bool> SwingStarted; // true = 차지 스윙
        public event Action<float> ChargeRatioChanged;

        public Phase Current { get; private set; } = Phase.Idle;

        /// 차지가 차고 있는 중인지. 이동 배율을 깎는 근거라 PlayerActionState가 읽는다.
        public bool IsCharging { get; private set; }

        public float ChargeRatio => _chargeTime <= 0f ? 1f : Mathf.Clamp01(_input.AttackHoldTime / _chargeTime);

        /// 한 스윙에서 이미 때린 대상. 액티브가 여러 프레임이라 중복 타격을 막아야 한다.
        readonly HashSet<IDamageable> _hitThisSwing = new HashSet<IDamageable>();
        readonly Collider[] _overlapBuffer = new Collider[32];

        float _phaseEndTime;
        bool _swingIsCharged;
        bool _harvestLock;
        float _nextHarvestTime;

        void Awake()
        {
            if (_input == null) _input = GetComponent<GameInput>();
            if (_state == null) _state = GetComponent<PlayerActionState>();
            if (_power == null) _power = GetComponent<HarvestPower>();
            if (_weapon == null) _weapon = GetComponent<PlayerWeapon>();
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

        void TickHarvest()
        {
            if (Time.time < _nextHarvestTime) return;

            IHarvestable target = FindNearestHarvestable(_range);
            if (target == null) return;

            _nextHarvestTime = Time.time + _harvestInterval;
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
                    return;
            }
        }

        void CancelSwing()
        {
            Current = Phase.Idle;
            _hitThisSwing.Clear();
            _harvestLock = false;
            SetCharging(false);
        }

        void ApplyHits()
        {
            float range = _swingIsCharged ? _chargeRange : _range;
            float halfAngle = _swingIsCharged ? 180f : _halfAngle; // 차지는 원형 360°
            int damage = Mathf.RoundToInt((_weapon != null ? _weapon.Damage : 1)
                                          * (_swingIsCharged ? _chargeDamageMultiplier : 1f));

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
