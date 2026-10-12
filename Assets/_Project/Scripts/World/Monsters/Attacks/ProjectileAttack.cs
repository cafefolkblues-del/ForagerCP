using UnityEngine;

namespace ForagerCP
{
    /// 투사체를 띄운다. 맞히는 일은 날아가는 쪽이 한다 — 쏜 뒤에 플레이어가 움직이므로
    /// 발사 시점에 명중을 정하면 피할 수가 없다.
    [CreateAssetMenu(menuName = "ForagerCP/몬스터 공격/투사체", fileName = "Attack_Projectile")]
    public class ProjectileAttack : MonsterAttack
    {
        [Header("투사체")]
        [SerializeField] GameObject _prefab;
        [SerializeField] float _speed = 8f;

        /// 이 거리를 날면 사라진다. 사거리와 따로 두는 이유: 쏜 뒤 플레이어가 물러나면
        /// 사거리보다 멀리 날아가야 맞는다.
        [SerializeField] float _travelDistance = 12f;

        /// 쏘는 높이. 0이면 몬스터 발밑에서 나가 바닥에 박힌다.
        [SerializeField] float _muzzleHeight = 0.6f;

        public override bool CanUse(in MonsterAttackContext context)
            => _prefab != null && base.CanUse(context);

        public override void Execute(in MonsterAttackContext context)
        {
            if (_prefab == null || context.TargetTransform == null) return;

            Vector3 origin = context.Self.position + Vector3.up * _muzzleHeight;

            Vector3 aim = context.TargetTransform.position + Vector3.up * _muzzleHeight - origin;
            aim.y = 0f;
            if (aim.sqrMagnitude < 0.0001f) aim = context.Self.forward;

            GameObject instance = PoolManager.Spawn(_prefab, origin, Quaternion.LookRotation(aim.normalized));
            if (instance == null) return;

            if (!instance.TryGetComponent(out Projectile projectile))
            {
                // 프리팹이 잘못 끼워졌다. 조용히 넘기면 '왜 안 맞지'를 한참 찾게 된다.
                Debug.LogWarning($"{name}: 투사체 프리팹에 Projectile이 없습니다", _prefab);
                PoolManager.Despawn(instance);
                return;
            }

            projectile.Launch(aim.normalized, _speed, _travelDistance, Damage, Kind, context.Self.gameObject);
        }

        public override string Describe()
            => base.Describe() + $" · 투사체 {_speed:0.#}m/s";
    }
}
