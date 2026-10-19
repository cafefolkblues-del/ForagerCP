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
        /// 막아냈다. 두 번째 값은 막아낸 상대 — 막히면 공격자가 잠시 멈추는 규칙이 있어서,
        /// 누구를 막았는지 없이는 그 규칙을 쓸 수가 없다.
        public event Action<PlayerHealth, GameObject> Blocked;

        public bool IsAlive => _hp > 0;
        public Transform Transform => transform;
        public int CurrentHp => _hp;
        /// 방어구가 올린 몫까지 더한 값. 최대 체력을 묻는 곳은 전부 여기를 봐야
        /// 갑옷을 낀 뒤 체력바가 옛 칸 수로 남는 일이 없다.
        public int MaxHp => _maxHp + (_equipment != null ? _equipment.MaxHpBonus : 0);

        /// 받는 피해를 몇 % 깎는가(최대 90). 스탯 창도 이 값을 읽는다.
        ///
        /// 출처(방어구·버프)마다 따로 곱하지 않고 비율을 먼저 더한 뒤 한 번만 적용한다.
        /// 따로 곱하면 둘만 겹쳐도 체감이 과하게 세진다.
        public int DamageReductionPercent => Mathf.Clamp(
            (_equipment != null ? _equipment.DamageReduction : 0) + (_buffs != null ? _buffs.DamageReduction : 0),
            0, 90);

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
                Blocked?.Invoke(this, source);

                // 막아낸 뒤 밀려날지. 기본은 완전 차단(앞서 확정된 쪽)이고, 기획이 원하면 켜면 된다.
                if (_knockbackWhenBlocked && _knockback != null) _knockback.ApplyFrom(source.transform);
                return;
            }

            // 방어구는 들어온 피해를 비율로 깎는다. 다만 0으로는 못 만든다 —
            // 방어구만으로 무적이 되면 전투가 성립하지 않는다(기획도 최소 피해 1로 못박았다).
            float scale = 1f - DamageReductionPercent / 100f;
            int taken = Mathf.Max(1, Mathf.RoundToInt(amount * scale));

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

        /// 저장된 자리가 막혀 있을 때 밀어낼 거리와 시도 횟수.
        /// 광물은 저장하지 않으므로, 캔 자리에 서서 저장했다가 불러오면
        /// 그 사이 되살아난 광물 안에서 깨어날 수 있다(3차 프로토타입 피드백 5번).
        [SerializeField] float _unstickStep = 0.6f;
        [SerializeField] int _unstickRings = 4;

        public string SaveKey => "player";

        /// 겹치지 않는 가장 가까운 자리. 저장된 자리가 비어 있으면 그대로 돌려준다.
        ///
        /// 광물 쪽은 '막혀 있으면 미룬다'로 막지만 그것만으로는 모자란다 —
        /// 광물이 먼저 되살아난 뒤에 플레이어가 그 위로 복원되는 순서도 있다.
        /// 양쪽을 다 막아야 순서에 기대지 않는다.
        Vector3 FreeSpotNear(Vector3 wanted)
        {
            if (!IsBlockedAt(wanted)) return wanted;

            for (int ring = 1; ring <= Mathf.Max(1, _unstickRings); ring++)
            {
                float radius = ring * _unstickStep;

                // 여덟 방향을 같은 거리에서 먼저 본다. 가까운 자리부터 집어야
                // 불러오자마자 엉뚱한 곳에 서 있지 않는다.
                for (int step = 0; step < 8; step++)
                {
                    float angle = step * Mathf.PI * 0.25f;
                    var candidate = wanted + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                    if (!IsBlockedAt(candidate)) return candidate;
                }
            }

            // 전부 막혔으면 원래 자리를 돌려준다. 못 찾았다고 맵 밖으로 보내는 것보다 낫다.
            return wanted;
        }

        bool IsBlockedAt(Vector3 position)
        {
            if (!TryGetComponent(out CapsuleCollider capsule)) return false;

            float radius = capsule.radius * 0.9f;
            Vector3 center = position + capsule.center;
            Vector3 up = Vector3.up * Mathf.Max(0f, capsule.height * 0.5f - capsule.radius);

            int count = Physics.OverlapCapsuleNonAlloc(
                center - up, center + up, radius, _unstickBuffer, ~0, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                if (_unstickBuffer[i] == null) continue;

                // 자기 자신과 바닥은 막는 것이 아니다.
                if (_unstickBuffer[i].transform.IsChildOf(transform)) continue;
                if (_unstickBuffer[i].GetComponentInParent<PlayerHealth>() != null) continue;
                if (_unstickBuffer[i].GetComponentInParent<MineralNode>() == null
                    && _unstickBuffer[i].GetComponentInParent<Monster>() == null
                    && _unstickBuffer[i].GetComponentInParent<GridPlacement>() == null) continue;

                return true;
            }
            return false;
        }

        readonly Collider[] _unstickBuffer = new Collider[8];

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

            transform.position = FreeSpotNear(save.Position);
            if (TryGetComponent(out Rigidbody placed)) placed.position = transform.position;
            Physics.SyncTransforms();

            // 불러오자마자 맞지 않도록 잠깐 무적을 준다.
            _invulnerableUntil = Time.time + _invulnerableTime;
            Changed?.Invoke(this);
        }
    }
}
