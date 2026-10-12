using UnityEngine;

namespace ForagerCP
{
    /// 채집력을 올린다. 수치로 떨어지는 스킬의 본보기.
    [CreateAssetMenu(menuName = "ForagerCP/스킬 효과/채집력", fileName = "Skill_Harvest")]
    public class HarvestPowerSkillEffect : SkillEffect
    {
        [SerializeField] int _bonus = 2;

        public override void Apply(SkillContext context)
        {
            if (context.Harvest == null) return;
            context.Harvest.AddBonus(_bonus);
        }

        public override string Describe() => $"채집력 +{_bonus}";
    }
}
