namespace ForagerCP
{
    /// 예고 칸. 공격을 고른 뒤 내지르기 전까지.
    ///
    /// 기본 동작은 제자리에서 표적만 바라보기다. 쫓아가며 때리면 예고가 피할 시간이 못 된다.
    ///
    /// Enter를 봉인(sealed)한 이유: 예고 연출이 듣는 Telegraphing 이벤트는 어떤 몹이든 반드시 나가야 한다.
    /// 하위 클래스는 OnEnter를 덮어쓴다.
    public abstract class WindupState : MonsterState
    {
        public sealed override void Enter(MonsterContext ctx)
        {
            ctx.Brain.RaiseTelegraphing(ctx.Memory.Winding);
            OnEnter(ctx);
        }

        protected virtual void OnEnter(MonsterContext ctx) { }

        public override StateId Tick(MonsterContext ctx)
        {
            ctx.Motor.Face(ctx.ToTarget);
            if (ctx.Now < ctx.Memory.WindupEndsAt) return StateId.Windup;

            ctx.Fire(ctx.Memory.WindingIndex);
            return ctx.AfterFire();
        }

        public override bool Committed => true;
    }
}
