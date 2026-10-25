using UnityEngine;

namespace ForagerCP
{
    /// 몹 한 종류의 행동 조립표. 칸마다 어떤 상태 구현을 쓸지만 고른다 — 로직도 수치도 없다.
    ///
    /// 구역별 강화판(R1~R5)은 같은 패턴을 공유하고 수치(MonsterDefinition)만 다르다.
    /// 칸 타입이 정해져 있어(ChaseState 칸에는 추적만) 엉뚱한 상태를 끼울 수 없다.
    ///
    /// 필수 칸이 비어 있으면 기본 구현을 쓴다. 후퇴 칸은 선택이라 비어 있으면 후퇴하지 않는다.
    [CreateAssetMenu(menuName = "ForagerCP/몬스터 패턴", fileName = "Pattern")]
    public class MonsterPattern : ScriptableObject
    {
        [SerializeField] ChaseState _chase;
        [SerializeField] WindupState _windup;
        [SerializeField] RecoverState _recover;
        [SerializeField] CooldownWaitState _cooldownWait;

        [Tooltip("비우면 후퇴하지 않는다. 최소 사거리가 있는 공격을 가진 몹에 넣는다.")]
        [SerializeField] RetreatState _retreat;

        /// 칸 이름 → 구현. 공통 칸(Idle·Return·Stagger·AttackMotion)은 실행기가 들고 있어 null.
        /// pattern이 null이어도(패턴을 안 넣은 몹) 기본 구현을 돌려준다.
        public static MonsterState Resolve(MonsterPattern pattern, StateId id)
        {
            switch (id)
            {
                case StateId.Chase: return pattern != null && pattern._chase != null ? pattern._chase : Defaults.Chase;
                case StateId.Windup: return pattern != null && pattern._windup != null ? pattern._windup : Defaults.Windup;
                case StateId.Recover: return pattern != null && pattern._recover != null ? pattern._recover : Defaults.Recover;
                case StateId.CooldownWait:
                    return pattern != null && pattern._cooldownWait != null ? pattern._cooldownWait : Defaults.CooldownWait;
                case StateId.Retreat: return pattern != null ? pattern._retreat : null;
                default: return null;
            }
        }

        /// 칸이 비었을 때 쓰는 기본 구현. 에셋을 찾아다니지 않으려고 실행 중에 한 번 만든다.
        /// 무상태라 모든 몬스터가 같은 인스턴스를 써도 된다.
        static class Defaults
        {
            static ChaseState _chase;
            static WindupState _windup;
            static RecoverState _recover;
            static CooldownWaitState _cooldownWait;

            public static ChaseState Chase => _chase != null ? _chase : _chase = Make<ChaseStraight>();
            public static WindupState Windup => _windup != null ? _windup : _windup = Make<WindupStill>();
            public static RecoverState Recover => _recover != null ? _recover : _recover = Make<RecoverStill>();
            public static CooldownWaitState CooldownWait
                => _cooldownWait != null ? _cooldownWait : _cooldownWait = Make<CooldownWaitHold>();

            static T Make<T>() where T : ScriptableObject
            {
                var state = CreateInstance<T>();
                state.hideFlags = HideFlags.HideAndDontSave;
                return state;
            }
        }
    }
}
