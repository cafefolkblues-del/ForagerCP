using UnityEngine;

namespace ForagerCP
{
    /// 때려서 체력을 깎을 수 있는 것(몬스터, 플레이어).
    /// 광물의 IHarvestable과 일부러 분리한다 — 판정 경로는 공유하되 보상 처리가 섞이면 안 되기 때문.
    public interface IDamageable
    {
        bool IsAlive { get; }
        Transform Transform { get; }

        /// source: 누가 때렸는지. 어그로·반격 판정이 들어올 자리.
        void TakeDamage(int amount, GameObject source);
    }
}
