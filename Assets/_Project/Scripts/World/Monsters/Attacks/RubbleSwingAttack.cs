using UnityEngine;

namespace ForagerCP
{
    /// 돌무더기의 휘두르기. 예고 뒤 코앞을 때린다.
    ///
    /// 공격 클래스를 몹별로 두는 이유: '근접' 같은 공용 이름으로 묶으면 골렘을 고칠 때
    /// 돌무더기까지 같이 바뀐다. 지금은 골렘 내려치기(GolemSlamAttack)와 동작이 같지만
    /// 패턴이 갈라질 예정이라 처음부터 따로 둔다. 공용은 몸통 접촉(ContactAttack) 하나뿐이다.
    [CreateAssetMenu(menuName = "ForagerCP/몬스터 공격/돌무더기 휘두르기", fileName = "Attack_Rubble_Swing")]
    public class RubbleSwingAttack : MonsterAttack
    {
        [Header("휘두르기 범위")]
        [Tooltip("한쪽 각도. 60이면 전체 공격 각도는 120도.")]
        [SerializeField, Range(0f, 90f)]
        float _halfAngle = 60f;

        public override void Execute(in MonsterAttackContext context)
        {
            if (context.Target == null || !context.Target.IsAlive) return;

            // 윈드업이 있으면 때리는 순간에 사거리를 다시 본다 — 예고 중에 플레이어가 피했는데
            // 그대로 맞으면 예고가 '피할 수 있는 신호'가 아니게 된다.
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

            // 전방 부채꼴을 벗어나면 피해를 주지 않는다.
            if (toTarget.sqrMagnitude > 0.0001f)
            {
                float angle = Vector3.Angle(forward, toTarget);

                if (angle > _halfAngle) return;
            }

            context.Target.TakeDamage(Damage, context.Self.gameObject, Kind);
        }
    }
}
