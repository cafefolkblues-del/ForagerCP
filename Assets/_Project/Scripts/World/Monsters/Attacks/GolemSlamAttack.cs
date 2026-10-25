using UnityEngine;

namespace ForagerCP
{
    /// 광갑 골렘의 내려치기. 긴 예고 뒤 코앞을 때린다.
    ///
    /// 지금은 돌무더기 휘두르기와 동작이 같다. 골렘 패턴이 갈라질 자리라 따로 둔다
    /// (RubbleSwingAttack 주석 참고).
    [CreateAssetMenu(menuName = "ForagerCP/몬스터 공격/골렘 내려치기", fileName = "Attack_Golem_Slam")]
    public class GolemSlamAttack : MonsterAttack
    {
        public override void Execute(in MonsterAttackContext context)
        {
            if (context.Target == null || !context.Target.IsAlive) return;

            // 예고 중에 피했으면 맞지 않는다. 예고가 피할 수 있는 신호여야 한다.
            if (Windup > 0f && !CanUse(context)) return;

            context.Target.TakeDamage(Damage, context.Self.gameObject, Kind);
        }
    }
}
