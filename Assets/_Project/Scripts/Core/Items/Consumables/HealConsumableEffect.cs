using UnityEngine;

namespace ForagerCP
{
    /// 체력을 회복한다.
    [CreateAssetMenu(menuName = "ForagerCP/소모품 효과/회복", fileName = "Use_Heal")]
    public class HealConsumableEffect : ConsumableEffect
    {
        [SerializeField] int _amount = 6;

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

        public override void Use(ConsumableContext context) => context.Health.Heal(_amount);

        public override string Describe() => $"체력 {_amount} 회복";
    }
}
