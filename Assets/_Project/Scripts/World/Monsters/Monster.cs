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

        public event Action<Monster> Died;
        public event Action<Monster> Respawned;

        public bool IsAlive => _alive;
        public Transform Transform => transform;
        public MonsterDefinition Definition => _definition;
        public int CurrentHp => _hp;
        public Vector3 HomePosition { get; private set; }

        Renderer[] _renderers;
        Collider[] _colliders;
        Coroutine _respawnRoutine;
        bool _alive = true;
        int _hp;

        void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _colliders = GetComponentsInChildren<Collider>(true);
            if (_hitFlash == null) _hitFlash = GetComponent<HitFlash>();

            HomePosition = transform.position;

            if (_definition == null)
            {
                Debug.LogError($"{name}: 몬스터 종류(MonsterDefinition)가 비어 있음", this);
                _alive = false;
                return;
            }

            _hp = _definition.MaxHp;
        }

        public void TakeDamage(int amount, GameObject source)
        {
            if (!_alive || amount <= 0) return;

            _hp -= amount;
            if (_hitFlash != null) _hitFlash.Play();

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

            transform.position = HomePosition;
            _hp = _definition.MaxHp;
            _alive = true;
            SetPresence(true);
            Respawned?.Invoke(this);
        }

        void SetPresence(bool visible)
        {
            for (int i = 0; i < _renderers.Length; i++) _renderers[i].enabled = visible;
            for (int i = 0; i < _colliders.Length; i++) _colliders[i].enabled = visible;
        }
    }
}
