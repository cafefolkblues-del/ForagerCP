using UnityEngine;

namespace ForagerCP
{
    /// 광석 돌진수의 공격 자리. 지금은 비어 있다 — 내질러도 아무 일도 없다.
    ///
    /// 임시로 만든 돌진(직선으로 들이받기)은 몹별 패턴을 다시 정하면서 폐기했다.
    /// 수치(피해·사거리·쿨다운·예고·빈틈)는 에셋에 그대로 있으므로 예고와 빈틈 흐름은 지금도 돈다.
    ///
    /// 다시 만들 때: 몸을 쓰는 공격이면 HasMotion을 true로 두고 TickMotion에서 매 프레임
    /// ctx.Motor로 움직인다. 진행 값은 ctx.Memory에 적는다(이 에셋은 R3~R5 돌진수가 공유한다).
    [CreateAssetMenu(menuName = "ForagerCP/몬스터 공격/돌진수 돌진", fileName = "Attack_Charger_Dash")]
    public class ChargerDashAttack : MonsterAttack
    {
        public override void Execute(in MonsterAttackContext context) { }

        public override string Describe() => base.Describe() + " · 돌진(미구현)";
    }
}
