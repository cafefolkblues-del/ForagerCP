namespace ForagerCP
{
    /// 몬스터 상태의 칸 이름. 상태는 다음 상태를 에셋이 아니라 이 이름으로 돌려준다.
    ///
    /// 에셋을 직접 가리키지 않는 이유: 예고 에셋이 "돌무더기용 빈틈 에셋"을 박아 두면 같은 예고를
    /// 다른 몹에 못 쓴다. 이름으로 돌려주면 그 칸에 무엇이 들어갈지는 몹의 MonsterPattern이 정한다.
    public enum StateId
    {
        // ---- 공통: 실행기(MonsterAI)가 직접 든다. 패턴 에셋에 칸이 없다 ----
        Idle,
        Return,
        Stagger,
        AttackMotion,

        // ---- 빈칸: 몹별 구현을 MonsterPattern에서 고른다 ----
        Chase,
        Windup,
        Recover,
        CooldownWait,

        /// 선택 칸. 패턴에 비어 있으면 후퇴하지 않는다.
        Retreat,
    }
}
