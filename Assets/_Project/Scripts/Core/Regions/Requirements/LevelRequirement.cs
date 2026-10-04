using UnityEngine;

namespace ForagerCP
{
    /// 레벨 N 이상이어야 들어간다. 자격만 보므로 치르는 것은 없다.
    [CreateAssetMenu(menuName = "ForagerCP/지역 조건/레벨", fileName = "Require_Level")]
    public class LevelRequirement : RegionRequirement
    {
        [SerializeField] int _minLevel = 2;

        public override bool IsMet(RegionContext context)
            => context.Level != null && context.Level.Level >= _minLevel;

        public override string Describe() => $"레벨 {_minLevel} 이상 필요";
    }
}
