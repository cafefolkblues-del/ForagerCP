using UnityEngine;

namespace ForagerCP
{
    /// 들어온 공격의 성격. 방패가 막아낼 수 있는지가 갈린다.
    ///
    /// 종류별 클래스를 두지 않는 이유: 지금 필요한 구분은 '막히느냐'뿐이고,
    /// 몬스터 유형별 특수공격은 아직 스펙이 없다. 값이 더 필요해지면 여기에 늘린다.
    public enum DamageKind
    {
        /// 보통 공격. 방패 정면이면 막힌다.
        Normal,

        /// 방어불가. 정면에서 막아도 그대로 들어온다.
        Unblockable,
    }

    /// 때려서 체력을 깎을 수 있는 것(몬스터, 플레이어).
    /// 광물의 IHarvestable과 일부러 분리한다 — 판정 경로는 공유하되 보상 처리가 섞이면 안 되기 때문.
    public interface IDamageable
    {
        bool IsAlive { get; }
        Transform Transform { get; }

        /// source: 누가 때렸는지. 어그로·반격 판정이 들어올 자리.
        /// kind: 기본값이 있어 기존 호출부는 그대로 둬도 된다 — 방어불가를 쓰는 쪽만 명시하면 된다.
        void TakeDamage(int amount, GameObject source, DamageKind kind = DamageKind.Normal);
    }
}
