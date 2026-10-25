namespace ForagerCP
{
    /// 빈틈 칸. 내지른 뒤, 또는 방패에 막힌 뒤 이동도 다른 공격도 없는 구간 — 플레이어의 반격 창이다.
    ///
    /// 끝나는 시각은 ctx.Memory.RecoverUntil에 있다(공격의 Recovery, 막히면 StaggerWhenBlocked로 늘어난다).
    /// 기본 동작은 제자리에서 바라보기.
    public abstract class RecoverState : MonsterState
    {
        public override StateId Tick(MonsterContext ctx)
        {
            ctx.Motor.Face(ctx.ToTarget);
            return ctx.Now < ctx.Memory.RecoverUntil ? StateId.Recover : StateId.Chase;
        }

        public override bool Committed => true;
    }
}
