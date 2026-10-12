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
        ///
        /// ⚠️ 이 판정은 효과 종류가 늘 때마다 같이 늘어난다. 기획이 새 소모품을 들고 오면
        /// "언제 써도 소용없는가"를 반드시 여기서 정해야 한다 — 안 정하면 기본값이 '언제나 쓸 수 있음'이라
        /// 아이템만 조용히 닳는다. 지금까지 나온 것:
        ///   회복 → 체력이 가득할 때 / 버프 → (쌓지 않고 시간만 채우므로 언제나 유효)
        /// 앞으로 올 만한 것: 이동 편의(이미 그 효과 중)·해독(걸린 상태가 없을 때)·소환(이미 소환됨) 등.
        public virtual bool CanUse(ConsumableContext context, out string reason)
        {
            reason = null;
            return true;
        }

        public abstract void Use(ConsumableContext context);

        public abstract string Describe();
    }
}
