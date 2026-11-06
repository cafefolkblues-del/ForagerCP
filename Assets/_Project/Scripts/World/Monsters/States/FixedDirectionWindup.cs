using UnityEngine;

namespace ForagerCP
{
    /// 골렘·돌무더기: 준비 시작 때 바라본 방향으로 제자리 공격한다.
    [CreateAssetMenu(menuName = "ForagerCP/몬스터 상태/예고 - 방향 고정", fileName = "State_FixedDirectionWindup")]
    public class FixedDirectionWindup : WindupState
    {
        protected override void OnEnter(MonsterContext ctx)
        {
            Vector3 direction = ctx.ToTarget;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = ctx.Monster.transform.forward;
                direction.y = 0f;
            }
            if (direction.sqrMagnitude < 0.0001f) direction = Vector3.forward;
            ctx.Memory.WindupDirection = direction.normalized;
            ctx.Motor.Face(ctx.Memory.WindupDirection);
        }

        public override StateId Tick(MonsterContext ctx)
        {
            ctx.Motor.Face(ctx.Memory.WindupDirection);
            if (ctx.Now < ctx.Memory.WindupEndsAt) return StateId.Windup;

            // Fire가 준비 값을 지우기 전에 몸의 정면을 고정했다. 기존 공격은 이 정면으로 판정한다.
            ctx.Fire(ctx.Memory.WindingIndex);
            return ctx.AfterFire();
        }
    }
}
