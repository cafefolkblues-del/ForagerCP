namespace ForagerCP
{
    /// 후퇴 칸(선택). 패턴에 비어 있으면 그 몹은 후퇴하지 않는다.
    ///
    /// 기본 동작: 표적이 최소 사거리(RetreatRange) 안이면 표적 반대쪽으로 한 걸음.
    /// 물러날 거리를 따로 두지 않는 이유: "안 쏘는 거리"와 "물러나는 거리"가 어긋나면
    /// 쏘지도 물러나지도 않고 멍하니 서 있는 구간이 생긴다.
    /// 기획이 "구역 밖으로 이동하지 않는다"로 못박아서, 구역을 벗어나는 걸음이면 물러나지 않는다.
    public abstract class RetreatState : MonsterState
    {
        /// 지금 물러나야 하고, 물러날 수 있는가. 추적·대기 칸이 이걸 묻고 후퇴로 넘어온다.
        /// 물러날 수 없으면(구석) false라 추적 칸이 공격·대기를 정한다.
        public virtual bool WantsToRetreat(MonsterContext ctx)
            => ctx.Distance < ctx.Definition.RetreatRange
               && ctx.Motor.CanStepWithin(-ctx.ToTarget, ctx.Definition.MoveSpeed, ctx.Aggro.HomeRegion);

        public override StateId Tick(MonsterContext ctx)
        {
            if (!WantsToRetreat(ctx)) return StateId.Chase;

            ctx.Motor.TryStepWithin(-ctx.ToTarget, ctx.Definition.MoveSpeed, ctx.Aggro.HomeRegion);
            ctx.Motor.Face(ctx.ToTarget);
            return StateId.Retreat;
        }
    }
}
