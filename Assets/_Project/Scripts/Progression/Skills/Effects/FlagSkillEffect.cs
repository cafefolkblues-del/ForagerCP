using UnityEngine;

namespace ForagerCP
{
    /// 진행 기록에 플래그를 세운다. "기능이 열린다" 류의 스킬은 전부 이걸로 표현된다.
    ///
    /// 기능마다 전용 효과 클래스를 만들지 않는 이유: 기획이 어떤 기능을 스킬로 뺄지 아직 모른다.
    /// 플래그면 디자이너가 에셋에 문자열만 적으면 되고, 받는 쪽은 ProgressFlags.Has만 보면 된다.
    /// 지역 출입 조건(ProgressFlagRequirement)도 같은 플래그를 읽으므로 "스킬로 구역 해금"이 그냥 된다.
    [CreateAssetMenu(menuName = "ForagerCP/스킬 효과/기능 해금", fileName = "Skill_Unlock")]
    public class FlagSkillEffect : SkillEffect
    {
        [SerializeField] string _flag = "attack_multi";

        /// 화면에 띄울 문구. 플래그 이름은 내부용이라 그대로 보여주면 안 된다.
        [SerializeField] string _description = "평타가 여러 적을 함께 때립니다";

        public string Flag => _flag;

        public override void Apply(SkillContext context)
        {
            if (context.Flags == null || string.IsNullOrEmpty(_flag)) return;
            context.Flags.Raise(_flag);
        }

        public override string Describe() => string.IsNullOrEmpty(_description) ? _flag : _description;
    }
}
