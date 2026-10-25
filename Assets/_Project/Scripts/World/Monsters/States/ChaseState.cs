namespace ForagerCP
{
    /// 추적 칸. 표적이 보이는 동안 "다가갈까, 물러날까, 칠까, 기다릴까"를 정한다.
    ///
    /// 기본 동작은 직선 접근이다. 몹마다 접근 방식(돌아서 오기, 거리 유지 등)이 다르면
    /// 이 클래스를 상속해 Tick을 덮어쓴다. 판단 순서(후퇴 → 공격 → 대기 → 접근)를 바꾸지 않는다면
    /// Approach만 덮어써도 된다.
    public abstract class ChaseState : MonsterState
    {
        public override StateId Tick(MonsterContext ctx)
        {
            RetreatState retreat = ctx.Retreat;
            if (retreat != null && retreat.WantsToRetreat(ctx)) return StateId.Retreat;

            int choice = ctx.PickAttack();
            if (choice >= 0)
            {
                ctx.Motor.Face(ctx.ToTarget);
                return ctx.StartAttack(choice);
            }

            // 사거리 안인데 쓸 것이 없으면(재사용 대기) 더 다가가지 않는다.
            // 다가가면 원거리 몬스터가 코앞까지 붙는다.
            if (ctx.Distance <= ctx.Definition.MaxAttackRange)
            {
                ctx.Motor.Face(ctx.ToTarget);
                return StateId.CooldownWait;
            }

            Approach(ctx);
            return StateId.Chase;
        }

        /// 한 프레임 다가가기. 기본은 표적을 보며 직선으로.
        protected virtual void Approach(MonsterContext ctx)
        {
            ctx.Motor.Face(ctx.ToTarget);
            ctx.Motor.Step(ctx.ToTarget, ctx.Definition.MoveSpeed);
        }
    }
}
