using UnityEngine;

namespace ForagerCP
{
    /// 소모품 효과가 건드릴 수 있는 것들.
    public class ConsumableContext
    {
        public PlayerHealth Health;
        public PlayerBuffs Buffs;
        public Inventory Inventory;
    }

    /// 소모품을 쓸 때 일어나는 일 하나.
    public abstract class ConsumableEffect : ScriptableObject
    {
        /// 지금 쓸 이유가 있는지. 체력이 꽉 찼으면 회복약은 거짓을 돌려준다.
        public virtual bool CanUse(ConsumableContext context, out string reason)
        {
            reason = null;
            return true;
        }

        public abstract void Use(ConsumableContext context);

        public abstract string Describe();
    }
}
