using UnityEngine;

namespace ForagerCP
{
    /// 후퇴 기본 구현: 최소 사거리 안이면 구역 안에서 뒤로 한 걸음씩 물러난다. 지금은 광맥 투척꾼이 쓴다.
    [CreateAssetMenu(menuName = "ForagerCP/몬스터 상태/후퇴 - 뒤로", fileName = "State_RetreatBackAway")]
    public class RetreatBackAway : RetreatState { }
}
