using UnityEngine;

namespace ForagerCP
{
    [CreateAssetMenu(menuName = "ForagerCP/몬스터 상태/추적 - 투척꾼 거리 유지", fileName = "State_ThrowerChase")]
    public class ThrowerChase : ChaseState
    {
        [SerializeField, Min(0.5f)] float _preferredDistance = 4f;
        [SerializeField, Min(0.5f)] float _approachDistance = 6f;

        public void Move(MonsterContext ctx)
        {
            ctx.Memory.ThrowerNavigation ??= new ThrowerNavigation();
            ctx.Memory.ThrowerNavigation.Tick(ctx, _preferredDistance, Mathf.Max(_preferredDistance, _approachDistance));
        }

        public override StateId Tick(MonsterContext ctx)
        {
            Move(ctx);
            ctx.Motor.Face(ctx.ToTarget);
            int choice = ctx.PickAttack();
            return choice >= 0 ? ctx.StartAttack(choice) : StateId.CooldownWait;
        }
    }
}
