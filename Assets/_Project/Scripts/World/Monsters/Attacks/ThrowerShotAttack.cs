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

        /// 한 번에 나가는 발수. 보스의 "직선 투사체 3발"이 이것이다.
        /// 피해는 발당이다 — 3발이면 다 맞았을 때 Damage의 세 배가 들어간다.
        [SerializeField] int _count = 1;

        /// 부챗살로 벌리는 전체 각도. 0이면 전부 같은 방향으로 겹쳐 나간다.
        [SerializeField] float _spreadDegrees;

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
            aim.Normalize();

            int shots = Mathf.Max(1, _count);

            // 겨눈 방향을 가운데에 두고 좌우로 고르게 벌린다. 한 발이면 각도 계산이 0이 되어
            // 그냥 정면으로 나간다 — 발수에 따라 분기를 두지 않으려고 식 하나로 맞춰 뒀다.
            float stepAngle = shots > 1 ? _spreadDegrees / (shots - 1) : 0f;
            float startAngle = shots > 1 ? -_spreadDegrees * 0.5f : 0f;

            for (int i = 0; i < shots; i++)
            {
                Vector3 direction = Quaternion.Euler(0f, startAngle + stepAngle * i, 0f) * aim;
                Fire(direction, origin, context);
            }
        }

        void Fire(Vector3 direction, Vector3 origin, in MonsterAttackContext context)
        {
            GameObject instance = PoolManager.Spawn(_prefab, origin, Quaternion.LookRotation(direction));
            if (instance == null) return;

            if (!instance.TryGetComponent(out Projectile projectile))
            {
                // 프리팹이 잘못 끼워졌다. 조용히 넘기면 '왜 안 맞지'를 한참 찾게 된다.
                Debug.LogWarning($"{name}: 투사체 프리팹에 Projectile이 없습니다", _prefab);
                PoolManager.Despawn(instance);
                return;
            }

            projectile.Launch(direction, _speed, _travelDistance, Damage, Kind, context.Self.gameObject);
        }

        public override string Describe()
            => base.Describe() + $" · 투사체 {_speed:0.#}m/s"
               + (_count > 1 ? $" {_count}발" : string.Empty)
               + (_spreadDegrees > 0f ? $" (부챗살 {_spreadDegrees:0.#}°)" : string.Empty);
    }
}
