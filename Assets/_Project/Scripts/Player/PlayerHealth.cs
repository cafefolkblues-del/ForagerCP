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
    public class PlayerHealth : MonoBehaviour, IDamageable, ISaveable
    {
        [SerializeField] int _maxHp = 20;
        [SerializeField] Transform _respawnPoint;
        [SerializeField] HitFlash _hitFlash;
        [SerializeField] Knockback _knockback;
        [SerializeField] PlayerShield _shield;
        [SerializeField] PlayerDash _dash;
        [SerializeField] PlayerEquipment _equipment;
        [SerializeField] PlayerBuffs _buffs;

        /// 연속 피격으로 즉사하는 걸 막는 최소한의 무적. 수치는 튜닝용.
        [SerializeField] float _invulnerableTime = 1f;

        /// 막아냈을 때도 밀려날지. 끄면 데미지도 넉백도 없는 완전 차단.
        [SerializeField] bool _knockbackWhenBlocked;

        public event Action<PlayerHealth> Changed;
        public event Action<PlayerHealth> Died;

        /// 방패로 막아낸 순간. 막기 연출·사운드가 붙을 자리.
        public event Action<PlayerHealth> Blocked;

        public bool IsAlive => _hp > 0;
        public Transform Transform => transform;
        public int CurrentHp => _hp;
        /// 방어구가 올린 몫까지 더한 값. 최대 체력을 묻는 곳은 전부 여기를 봐야
        /// 갑옷을 낀 뒤 체력바가 옛 칸 수로 남는 일이 없다.
        public int MaxHp => _maxHp + (_equipment != null ? _equipment.MaxHpBonus : 0);

        int _hp;
        float _invulnerableUntil;

        void Awake()
        {
            _hp = MaxHp;
            if (_hitFlash == null) _hitFlash = GetComponent<HitFlash>();
            if (_knockback == null) _knockback = GetComponent<Knockback>();
            if (_shield == null) _shield = GetComponent<PlayerShield>();
            if (_dash == null) _dash = GetComponent<PlayerDash>();
            if (_equipment == null) _equipment = FindFirstObjectByType<PlayerEquipment>();
            if (_buffs == null) _buffs = FindFirstObjectByType<PlayerBuffs>();
        }

        void Start() => Changed?.Invoke(this);

        public void TakeDamage(int amount, GameObject source, DamageKind kind = DamageKind.Normal)
        {
            if (!IsAlive || amount <= 0) return;
            if (Time.time < _invulnerableUntil) return;

            // 대시 무적(기본 꺼짐). 기획이 원하면 PlayerDash에서 시간만 올리면 여기 코드는 그대로다.
            if (_dash != null && _dash.IsInvulnerable) return;

            // 방패는 '정면에서 온 일반공격'만 걷어낸다.
            // 방어불가는 각도를 따지기 전에 통과시킨다 — 막는 시늉조차 하면 안 되므로 Blocks도 부르지 않는다
            // (Blocks는 성공하면 Blocked 이벤트를 쏘기 때문에, 불러두고 결과만 무시하면 연출이 잘못 나간다).
            if (kind == DamageKind.Normal
                && _shield != null && source != null
                && _shield.Blocks(source.transform.position))
            {
                Blocked?.Invoke(this);

                // 막아낸 뒤 밀려날지. 기본은 완전 차단(앞서 확정된 쪽)이고, 기획이 원하면 켜면 된다.
                if (_knockbackWhenBlocked && _knockback != null) _knockback.ApplyFrom(source.transform);
                return;
            }

            // 방어구는 들어온 피해를 깎는다. 다만 0으로는 못 만든다 —
            // 방어구만으로 무적이 되면 전투가 성립하지 않는다.
            int reduction = (_equipment != null ? _equipment.DamageReduction : 0)
                          + (_buffs != null ? _buffs.DamageReduction : 0);
            int taken = Mathf.Max(1, amount - reduction);

            _invulnerableUntil = Time.time + _invulnerableTime;
            _hp = Mathf.Max(0, _hp - taken);

            if (_hitFlash != null) _hitFlash.Play();
            if (_knockback != null && source != null) _knockback.ApplyFrom(source.transform);
            Changed?.Invoke(this);

            if (_hp <= 0) Die();
        }

        public void Heal(int amount)
        {
            if (amount <= 0 || !IsAlive) return;

            _hp = Mathf.Min(MaxHp, _hp + amount);
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

            _hp = MaxHp;
            _invulnerableUntil = Time.time + _invulnerableTime;
            Changed?.Invoke(this);
        }

        // ---------------- 저장 ----------------

        [Serializable]
        class Save
        {
            public int Hp;
            public Vector3 Position;
        }

        public string SaveKey => "player";

        public string Capture() => JsonUtility.ToJson(new Save { Hp = _hp, Position = transform.position });

        public void Restore(string payload)
        {
            var save = JsonUtility.FromJson<Save>(payload);
            if (save == null) return;

            _hp = Mathf.Clamp(save.Hp, 1, MaxHp);

            // transform만 옮기면 Rigidbody가 옛 자리로 되돌린다 — 몬스터 순간이동과 같은 함정.
            if (TryGetComponent(out Rigidbody body))
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.position = save.Position;
            }

            transform.position = save.Position;
            Physics.SyncTransforms();

            // 불러오자마자 맞지 않도록 잠깐 무적을 준다.
            _invulnerableUntil = Time.time + _invulnerableTime;
            Changed?.Invoke(this);
        }
    }
}
