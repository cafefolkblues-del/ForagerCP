using UnityEngine;

namespace ForagerCP
{
    /// 추적 기본 구현: 표적을 보며 직선으로 다가간다. 몹 전용 추적이 생기기 전까지 모든 몹이 쓴다.
    [CreateAssetMenu(menuName = "ForagerCP/몬스터 상태/추적 - 직선", fileName = "State_ChaseStraight")]
    public class ChaseStraight : ChaseState { }
}
