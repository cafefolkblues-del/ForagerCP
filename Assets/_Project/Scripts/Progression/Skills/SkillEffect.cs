using UnityEngine;

namespace ForagerCP
{
    /// 스킬 효과가 건드릴 수 있는 것들. 효과마다 필요한 데가 달라서 한 번에 넘긴다.
    /// RegionContext와 같은 이유 — 효과가 직접 찾아다니면 교체도 테스트도 어려워진다.
    public class SkillContext
    {
        public HarvestPower Harvest;
        public PlayerHealth Health;
        public ProgressFlags Flags;
        public Inventory Inventory;
    }

    /// 스킬 하나가 실제로 하는 일.
    ///
    /// 세부 스펙이 아직 없어서 종류를 모른다. 추상으로 두면 나중에 이 클래스를 상속한 에셋을
    /// 하나 더 만드는 것으로 끝나고, 격자도 보드도 화면도 건드리지 않는다.
    /// (지역 출입 조건을 RegionRequirement로 깐 것과 같은 수법)
    public abstract class SkillEffect : ScriptableObject
    {
        public abstract void Apply(SkillContext context);

        /// 화면에 그대로 띄울 설명. 효과가 자기를 설명해야 UI에 종류별 분기가 쌓이지 않는다.
        public abstract string Describe();
    }
}
