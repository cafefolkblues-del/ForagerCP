using UnityEngine;

namespace ForagerCP
{
    [CreateAssetMenu(menuName = "ForagerCP/몬스터 상태/예고 - 투척꾼 이동 투척", fileName = "State_ThrowerWindup")]
    public class ThrowerWindup : WindupState
    {
        public override StateId Tick(MonsterContext ctx)
        {
            (ctx.Brain.Resolve(StateId.Chase) as ThrowerChase)?.Move(ctx);
            return base.Tick(ctx);
        }
    }
}
