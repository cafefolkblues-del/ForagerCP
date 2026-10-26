using UnityEngine;

namespace ForagerCP
{
    /// 돌진 준비를 시작한 순간 방향을 고정한다. 피한 표적을 다시 겨누지 않는다.
    [CreateAssetMenu(menuName = "ForagerCP/몬스터 상태/예고 - 돌진 방향 고정", fileName = "State_ChargerWindup")]
    public class ChargerWindup : WindupState
    {
        protected override void OnEnter(MonsterContext ctx)
        {
            if (ctx.Memory.Winding is not ChargerDashAttack) return;

            Vector3 direction = ctx.ToTarget;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = ctx.Monster.transform.forward;
                direction.y = 0f;
            }
            if (direction.sqrMagnitude < 0.0001f) direction = Vector3.forward;

            // 공유 상태 에셋이 아니라 몬스터 한 마리의 진행 값에 저장한다.
            ctx.Memory.DashDirection = direction.normalized;
            ctx.Memory.DashTravelled = 0f;
            ctx.Memory.DashHasHit = false;
            ctx.Memory.DashInitialized = true;
        }

        public override StateId Tick(MonsterContext ctx)
        {
            if (ctx.Memory.Winding is not ChargerDashAttack) return base.Tick(ctx);

            ctx.Motor.Face(ctx.Memory.DashDirection);
            if (ctx.Now < ctx.Memory.WindupEndsAt) return StateId.Windup;

            // 준비 후에는 표적의 현재 위치나 사거리로 방향을 다시 정하지 않는다.
            ctx.Fire(ctx.Memory.WindingIndex);
            return ctx.AfterFire();
        }
    }
}
