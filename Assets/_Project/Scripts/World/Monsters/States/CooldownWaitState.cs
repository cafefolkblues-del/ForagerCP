namespace ForagerCP
{
    /// 쿨다운 대기 칸. 사거리 안인데 쓸 공격이 아직 재사용 대기 중일 때.
    ///
    /// 시간을 세는 것은 공통(ctx.Memory.ReadyAt)이고, 여기는 기다리는 동안 무엇을 할지만 정한다.
    /// 기본 동작은 제자리에서 바라보기. 몹마다 맴돌기·거리 벌리기 등이 생기면 상속해 덮어쓴다.
    public abstract class CooldownWaitState : MonsterState
    {
        public override StateId Tick(MonsterContext ctx)
        {
            ctx.Motor.Face(ctx.ToTarget);

            RetreatState retreat = ctx.Retreat;
            if (retreat != null && retreat.WantsToRetreat(ctx)) return StateId.Retreat;

            // 칠 수 있게 됐거나 사거리를 벗어났으면 추적 칸이 다시 정한다.
            if (ctx.Distance > ctx.Definition.MaxAttackRange || ctx.PickAttack() >= 0) return StateId.Chase;

            Wait(ctx);
            return StateId.CooldownWait;
        }

        /// 한 프레임 기다리기. 기본은 아무것도 안 함.
        protected virtual void Wait(MonsterContext ctx) { }
    }
}
