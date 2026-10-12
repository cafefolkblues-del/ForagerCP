using UnityEngine;

namespace ForagerCP
{
    /// 코앞을 바로 때린다. 지금 슬라임들이 쓰는 것.
    [CreateAssetMenu(menuName = "ForagerCP/몬스터 공격/근접", fileName = "Attack_Melee")]
    public class MeleeAttack : MonsterAttack
    {
        public override void Execute(in MonsterAttackContext context)
        {
            if (context.Target == null || !context.Target.IsAlive) return;

            // 윈드업이 있으면 때리는 순간에 사거리를 다시 본다 — 예고 중에 플레이어가 피했는데
            // 그대로 맞으면 예고가 '피할 수 있는 신호'가 아니게 된다.
            if (Windup > 0f && !CanUse(context)) return;

            context.Target.TakeDamage(Damage, context.Self.gameObject, Kind);
        }
    }
}
