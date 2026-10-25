using UnityEngine;

namespace ForagerCP
{
    /// 몬스터 상태 하나. 무상태 에셋이다 — 같은 종류의 몬스터 전부가 이 에셋 하나를 공유하므로
    /// 값을 여기 적으면 다섯 마리가 시계 하나를 나눠 쓴다. 진행 값은 ctx.Memory에 적는다.
    ///
    /// 직접 상속하지 않는다. 칸마다 있는 추상 클래스(ChaseState, WindupState …)를 상속한다 —
    /// 그래야 패턴 에셋의 칸에 엉뚱한 상태를 끼울 수 없고, 칸의 기본 동작을 물려받는다.
    ///
    /// 하지 않는 일: 위치를 직접 바꾸지 않는다(ctx.Motor), 경직·막힘·어그로를 판단하지 않는다(실행기 몫).
    public abstract class MonsterState : ScriptableObject
    {
        /// 이 칸에 들어왔을 때 한 번.
        public virtual void Enter(MonsterContext ctx) { }

        /// 매 물리 프레임. 다음 칸 이름을 돌려준다. 그대로 머물려면 자기 칸 이름.
        public abstract StateId Tick(MonsterContext ctx);

        /// 이 칸을 떠날 때 한 번.
        public virtual void Exit(MonsterContext ctx) { }

        /// 참이면 이 칸에 있는 동안 어그로 검사를 미룬다(표적을 잃어도 끝까지 한다).
        /// 예고·빈틈이 그렇다 — 휘두르다 말고 돌아가면 반격 창이 사라진다.
        public virtual bool Committed => false;
    }
}
