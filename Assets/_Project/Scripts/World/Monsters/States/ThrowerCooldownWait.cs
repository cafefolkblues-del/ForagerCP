using UnityEngine;

namespace ForagerCP
{
    [CreateAssetMenu(menuName = "ForagerCP/몬스터 상태/대기 - 투척꾼 거리 유지", fileName = "State_ThrowerCooldownWait")]
    public class ThrowerCooldownWait : CooldownWaitState
    {
        protected override void Wait(MonsterContext ctx)
            => (ctx.Brain.Resolve(StateId.Chase) as ThrowerChase)?.Move(ctx);
    }
}
