using UnityEngine;

namespace ForagerCP
{
    [CreateAssetMenu(menuName = "ForagerCP/몬스터 상태/회복 - 투척꾼 거리 유지", fileName = "State_ThrowerRecover")]
    public class ThrowerRecover : RecoverState
    {
        public override StateId Tick(MonsterContext ctx)
        {
            (ctx.Brain.Resolve(StateId.Chase) as ThrowerChase)?.Move(ctx);
            return base.Tick(ctx);
        }
    }
}
