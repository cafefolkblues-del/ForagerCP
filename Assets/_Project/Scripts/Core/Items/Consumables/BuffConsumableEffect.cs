using UnityEngine;

namespace ForagerCP
{
    /// 일정 시간 동안 수치를 올린다.
    [CreateAssetMenu(menuName = "ForagerCP/소모품 효과/버프", fileName = "Use_Buff")]
    public class BuffConsumableEffect : ConsumableEffect
    {
        [SerializeField] BuffDefinition _buff;

        public override bool CanUse(ConsumableContext context, out string reason)
        {
            reason = null;
            if (_buff == null || context.Buffs == null) return false;

            // 같은 버프가 이미 돌고 있으면 시간만 다시 채운다. 겹쳐 쌓지 않는 쪽이 기획 확인 전까지 안전하다.
            return true;
        }

        public override void Use(ConsumableContext context) => context.Buffs.Apply(_buff);

        public override string Describe() => _buff != null ? _buff.Describe() : "버프 미지정";
    }
}
