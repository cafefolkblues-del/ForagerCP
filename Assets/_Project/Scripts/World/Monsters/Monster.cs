using System;
using System.Collections;
using UnityEngine;

namespace ForagerCP
{
    /// 몬스터의 체력·사망·재등장. 이동과 공격은 MonsterAI가 맡는다(책임 분리).
    /// 광물과 같은 방식으로, 죽어도 오브젝트는 남기고 렌더러/콜라이더만 끈다.
    public class Monster : MonoBehaviour, IDamageable
    {
        [SerializeField] MonsterDefinition _definition;
        [SerializeField] HitFlash _hitFlash;
        [SerializeField] Knockback _knockback;

        public event Action<Monster> Died;
        public event Action<Monster> Respawned;

        public bool IsAlive => _alive;
        public Transform Transform => transform;
        public MonsterDefinition Definition => _definition;
        public int CurrentHp => _hp;
        public Vector3 HomePosition { get; private set; }

        Renderer[] _renderers;
        Collider[] _colliders;
        Rigidbody _body;
        Coroutine _respawnRoutine;
        bool _alive = true;
        int _hp;

        void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _colliders = GetComponentsInChildren<Collider>(true);
            _body = GetComponent<Rigidbody>();
            if (_hitFlash == null) _hitFlash = GetComponent<HitFlash>();
            if (_knockback == null) _knockback = GetComponent<Knockback>();

            if (_definition == null)
            {
                Debug.LogError($"{name}: 몬스터 종류(MonsterDefinition)가 비어 있음", this);
                _alive = false;
                return;
            }

            _hp = _definition.MaxHp;
        }

        /// 스폰 위치는 Awake가 아니라 Start에서 잡는다.
        /// GridPlacement가 OnEnable에서 칸 좌표로 위치를 맞추기 때문에, Awake 시점 값은 배치 전 좌표일 수 있다.
        void Start() => HomePosition = transform.position;

        public void TakeDamage(int amount, GameObject source)
        {
            if (!_alive || amount <= 0) return;

            _hp -= amount;
            if (_hitFlash != null) _hitFlash.Play();
            if (_knockback != null && source != null) _knockback.ApplyFrom(source.transform);

            if (_hp > 0) return;
            Die();
        }

        void Die()
        {
            _alive = false;
            _hp = 0;
            SetPresence(false);

            // 처치 보상(경험치)은 CombatRewardService가 받아서 처리한다.
            // 드랍 아이템은 기획 미확정이라 여기서 만들지 않는다.
            Died?.Invoke(this);

            if (_definition.RespawnDelay > 0f) _respawnRoutine = StartCoroutine(RespawnRoutine());
        }

        IEnumerator RespawnRoutine()
        {
            yield return new WaitForSeconds(_definition.RespawnDelay);
            _respawnRoutine = null;
            Respawn();
        }

        public void Respawn()
        {
            if (_definition == null) return;

            if (_respawnRoutine != null)
            {
                StopCoroutine(_respawnRoutine);
                _respawnRoutine = null;
            }

            _hp = _definition.MaxHp;
            _alive = true;
            SetPresence(true);
            TeleportTo(HomePosition);
            Respawned?.Invoke(this);
        }

        /// 체력은 그대로 두고 스폰 자리로만 돌려보낸다(맵 밖으로 빠졌을 때 구제용).
        public void ReturnHome() => TeleportTo(HomePosition);

        /// transform만 옮기면 Rigidbody 내부 위치가 옛 자리에 남아 물리가 그쪽으로 되돌린다.
        /// 위치·속도를 같이 리셋하고 SyncTransforms로 물리 쪽에 즉시 반영한다.
        void TeleportTo(Vector3 position)
        {
            if (_body != null)
            {
                _body.linearVelocity = Vector3.zero;
                _body.angularVelocity = Vector3.zero;
                _body.position = position;
            }

            transform.position = position;
            Physics.SyncTransforms();
        }

        void SetPresence(bool visible)
        {
            for (int i = 0; i < _renderers.Length; i++) _renderers[i].enabled = visible;
            for (int i = 0; i < _colliders.Length; i++) _colliders[i].enabled = visible;

            // 🐛 죽은 동안 콜라이더가 꺼져 바닥을 못 받치는데 중력은 그대로라 맵 아래로 계속 떨어졌다.
            // (리스폰 시간만큼 낙하 → 되살아나도 바닥 밑이라 안 보임)
            // 죽어 있는 동안은 물리에서 빼두고, 살아날 때 다시 넣는다.
            if (_body == null) return;

            _body.isKinematic = !visible;
            if (!visible) return;

            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;
        }
    }
}
