using UnityEngine;

namespace ForagerCP
{
    /// 광갑 골렘의 내려치기.
    /// 긴 예고 뒤 사거리 안의 전방 영역을 타격한다.
    [CreateAssetMenu(menuName = "ForagerCP/몬스터 공격/골렘 내려치기", fileName = "Attack_Golem_Slam")]
    public class GolemSlamAttack : MonsterAttack
    {
        [Header("내려찍기 범위")]
        [Tooltip("좌우 반폭. 0.8이면 전체 폭은 1.6m.")]
        [SerializeField, Min(0.01f)]
        float _halfWidth = 0.8f;

        public override void Execute(in MonsterAttackContext context)
        {
            if (context.Target == null || !context.Target.IsAlive) return;

            // 예고 중에 피했으면 맞지 않는다. 예고가 피할 수 있는 신호여야 한다.
            if (Windup > 0f && !CanUse(context)) return;
            if (context.Self == null) return;

            Vector3 toTarget =
                context.Target.Transform.position - context.Self.position;
            toTarget.y = 0f;

            Vector3 forward = Vector3.ProjectOnPlane(
                context.Self.forward,
                Vector3.up
            ).normalized;

            if (forward.sqrMagnitude < 0.0001f) return;

            Vector3 right = Vector3.Cross(Vector3.up, forward);

            float forwardDistance = Vector3.Dot(toTarget, forward);
            float sideDistance = Mathf.Abs(Vector3.Dot(toTarget, right));

            // 뒤쪽이거나 좌우 공격 폭을 벗어나면 피해를 주지 않는다.
            if (forwardDistance < 0f) return;
            if (sideDistance > _halfWidth) return;

            context.Target.TakeDamage(Damage, context.Self.gameObject, Kind);
        }
    }
}
