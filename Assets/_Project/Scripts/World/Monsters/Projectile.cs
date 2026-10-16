using UnityEngine;

namespace ForagerCP
{
    /// 날아가서 처음 맞히는 것 하나에 피해를 주고 사라진다.
    ///
    /// 물리 Rigidbody를 쓰지 않는 이유: 투사체가 몬스터·광물을 밀고 다니면 배치가 흐트러지고,
    /// 넉백·경직과 힘이 섞인다. 직접 움직이고 SphereCast로 맞힌다 — 빠른 탄이 벽을 뚫는 것도 막힌다.
    public class Projectile : MonoBehaviour, IPooled
    {
        [SerializeField] float _radius = 0.25f;

        /// 쏜 쪽을 맞히지 않게 거르는 것 외에, 이 층만 때린다. 기본은 전부.
        [SerializeField] LayerMask _hitMask = ~0;

        Vector3 _direction;
        float _speed;
        float _remaining;
        int _damage;
        DamageKind _kind;
        GameObject _source;
        bool _flying;

        public void Launch(Vector3 direction, float speed, float distance,
                           int damage, DamageKind kind, GameObject source)
        {
            _direction = direction.normalized;
            _speed = Mathf.Max(0.1f, speed);
            _remaining = Mathf.Max(0.1f, distance);
            _damage = damage;
            _kind = kind;
            _source = source;
            _flying = true;

            transform.rotation = Quaternion.LookRotation(_direction);
        }

        void Update()
        {
            if (!_flying) return;

            float step = _speed * Time.deltaTime;

            // 한 프레임에 지나갈 구간을 통째로 훑는다. 위치만 옮기고 겹침을 보면
            // 빠른 탄이 얇은 것을 그냥 지나친다.
            if (Physics.SphereCast(transform.position, _radius, _direction, out RaycastHit hit, step,
                    _hitMask, QueryTriggerInteraction.Ignore)
                && TryHit(hit.collider))
            {
                return;
            }

            transform.position += _direction * step;

            _remaining -= step;
            if (_remaining <= 0f) Finish();
        }

        /// 맞혔으면 true. 쏜 쪽과 같은 편은 뚫고 지나간다.
        /// 피해를 줄 때 넘기는 'source'는 쏜 몬스터가 아니라 탄 자신이다(아래 주석 참고).
        bool TryHit(Collider collider)
        {
            var damageable = collider.GetComponentInParent<IDamageable>();

            if (damageable != null)
            {
                // 쏜 몬스터 자신과, 같은 Monster 종류끼리는 맞지 않는다.
                // 자기 탄에 맞아 죽는 몬스터는 버그로만 보인다.
                if (_source != null && damageable.Transform.gameObject == _source) return false;
                if (damageable is Monster) return false;

                if (!damageable.IsAlive) return false;

                // 때린 쪽으로 '탄 자신'을 넘긴다. 쏜 몬스터를 넘기면 방패 각도를 몬스터
                // 위치로 재게 되어, 탄이 옆이나 뒤에서 날아와도 몬스터가 정면이면 막힌다.
                // 기획이 "뒤에서 맞으면 피해"를 명시하므로 판정 기준은 탄이어야 한다.
                // 넉백 방향도 같은 값을 쓰는데, 그쪽도 탄이 온 방향이 맞다.
                damageable.TakeDamage(_damage, gameObject, _kind);
                Finish();
                return true;
            }

            // 살아있는 것이 아니면 지형·구조물이다. 거기서 멈춘다.
            Finish();
            return true;
        }

        void Finish()
        {
            _flying = false;
            PoolManager.Despawn(gameObject);
        }

        // 풀에서 재사용되므로 이전 비행의 상태를 여기서 끊는다.
        public void OnSpawnedFromPool() => _flying = false;

        public void OnReturnedToPool() => _flying = false;
    }
}
