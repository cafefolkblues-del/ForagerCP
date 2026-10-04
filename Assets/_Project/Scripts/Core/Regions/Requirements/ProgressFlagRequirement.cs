using UnityEngine;

namespace ForagerCP
{
    /// 특정 사건이 일어나야 들어간다. 보스 처치처럼 '상태'가 아니라 '행동'이 조건일 때 쓴다.
    [CreateAssetMenu(menuName = "ForagerCP/지역 조건/진행 기록", fileName = "Require_Flag")]
    public class ProgressFlagRequirement : RegionRequirement
    {
        [SerializeField] string _flag = "boss_cleared";

        /// 화면에 띄울 문구. 플래그 이름은 내부용이라 그대로 보여주면 안 된다.
        [SerializeField] string _description = "첫 보스를 처치해야 합니다";

        public override bool IsMet(RegionContext context) => context.Flags != null && context.Flags.Has(_flag);

        public override string Describe() => string.IsNullOrEmpty(_description) ? _flag : _description;
    }
}
