using UnityEngine;

namespace ForagerCP
{
    /// 체력을 회복한다.
    [CreateAssetMenu(menuName = "ForagerCP/소모품 효과/회복", fileName = "Use_Heal")]
    public class HealConsumableEffect : ConsumableEffect
    {
        /// 최대 체력의 몇 %를 돌려주는가. 고정값으로 두지 않는 이유:
        /// 최대 체력이 스킬로 늘어나는데 고정 회복이면 후반에 약이 쓸모없어진다.
        /// 실제 회복량은 빈 만큼을 넘지 않는다(Heal이 알아서 자른다).
        [SerializeField] int _percentOfMax = 30;

        public override bool CanUse(ConsumableContext context, out string reason)
        {
            reason = null;
            if (context.Health == null) return false;

            // 꽉 찬 채로 마시면 약만 사라진다.
            if (context.Health.CurrentHp >= context.Health.MaxHp)
            {
                reason = "체력이 가득 찼습니다";
                return false;
            }
            return true;
        }

        public override void Use(ConsumableContext context)
            => context.Health.Heal(Mathf.Max(1, Mathf.RoundToInt(
                context.Health.MaxHp * Mathf.Clamp(_percentOfMax, 1, 100) / 100f)));

        public override string Describe() => $"체력 {_percentOfMax}% 회복";
    }
}
