using System;
using UnityEngine;

namespace ForagerCP
{
    /// 플레이어 체력·피격·사망. 사망하면 리스폰 지점으로 돌려보내고 체력을 채운다.
    ///
    /// ⚠️ 죽음 패널티는 기획 미확정(3장) — "광물 획득량 감소 디버프" 방향만 합의됐고
    /// 감소율·지속시간이 비어 있어서 여기서 정하지 않는다.
    /// 확정되면 Died 이벤트를 구독하는 컴포넌트를 하나 만들어 붙이면 되고, 이 파일은 안 건드려도 된다.
    ///   예) DeathPenaltyService.OnDied → 채집 배율 디버프 N초 부여
    ///   예) HudView.OnDied → 사망 연출 / 페이드
    ///   예) SoundService.OnDied → 사망음
    public class PlayerHealth : MonoBehaviour, IDamageable
    {
        [SerializeField] int _maxHp = 20;
        [SerializeField] Transform _respawnPoint;
        [SerializeField] HitFlash _hitFlash;
        [SerializeField] Knockback _knockback;

        /// 연속 피격으로 즉사하는 걸 막는 최소한의 무적. 수치는 튜닝용.
        [SerializeField] float _invulnerableTime = 0.4f;

        public event Action<PlayerHealth> Changed;
        public event Action<PlayerHealth> Died;

        public bool IsAlive => _hp > 0;
        public Transform Transform => transform;
        public int CurrentHp => _hp;
        public int MaxHp => _maxHp;

        int _hp;
        float _invulnerableUntil;

        void Awake()
        {
            _hp = _maxHp;
            if (_hitFlash == null) _hitFlash = GetComponent<HitFlash>();
            if (_knockback == null) _knockback = GetComponent<Knockback>();
        }

        void Start() => Changed?.Invoke(this);

        public void TakeDamage(int amount, GameObject source)
        {
            if (!IsAlive || amount <= 0) return;
            if (Time.time < _invulnerableUntil) return;

            _invulnerableUntil = Time.time + _invulnerableTime;
            _hp = Mathf.Max(0, _hp - amount);

            if (_hitFlash != null) _hitFlash.Play();
            if (_knockback != null && source != null) _knockback.ApplyFrom(source.transform);
            Changed?.Invoke(this);

            if (_hp <= 0) Die();
        }

        public void Heal(int amount)
        {
            if (amount <= 0 || !IsAlive) return;

            _hp = Mathf.Min(_maxHp, _hp + amount);
            Changed?.Invoke(this);
        }

        void Die()
        {
            Died?.Invoke(this);
            Respawn();
        }

        /// 사양 2-2: 사망 시 리스폰 위치로 이동. 디버그 위치 초기화도 같은 경로를 쓴다.
        public void Respawn()
        {
            if (_respawnPoint != null)
            {
                if (TryGetComponent(out Rigidbody body))
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
                transform.position = _respawnPoint.position;
            }

            _hp = _maxHp;
            _invulnerableUntil = Time.time + _invulnerableTime;
            Changed?.Invoke(this);
        }
    }
}
