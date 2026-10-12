using UnityEngine;

namespace ForagerCP
{
    /// 공격 한 번을 내지를 때 필요한 것들. 구조체로 넘기는 이유: 공격 에셋은 공유 자산이라
    /// 누구를 때리는지를 자기 안에 들고 있으면 안 된다(같은 에셋을 쓰는 몬스터가 여럿이다).
    public readonly struct MonsterAttackContext
    {
        public readonly Monster Monster;
        public readonly Transform Self;
        public readonly IDamageable Target;
        public readonly Transform TargetTransform;

        /// 평면 거리. 높이 차이를 섞으면 같은 칸인데 지형에 따라 사거리가 달라진다.
        public readonly float Distance;

        public MonsterAttackContext(Monster monster, IDamageable target, Transform targetTransform, float distance)
        {
            Monster = monster;
            Self = monster != null ? monster.transform : null;
            Target = target;
            TargetTransform = targetTransform;
            Distance = distance;
        }
    }

    /// 몬스터가 쓸 수 있는 공격 하나.
    ///
    /// '몬스터 유형'을 enum이나 클래스로 만들지 않은 이유: 유형이란 결국 공격·수치의 조합이고,
    /// 조합을 코드에 적으면 기획이 유형을 하나 더 들고 올 때마다 코드가 늘어난다.
    /// 공격을 에셋으로 두면 유형은 에셋 조합이 되어, 종류가 늘어도 상속 하나로 끝난다
    /// (RegionRequirement · SkillEffect · ShopEffect · ConsumableEffect와 같은 틀).
    public abstract class MonsterAttack : ScriptableObject
    {
        [SerializeField] string _displayName;

        [Header("수치")]
        [SerializeField] int _damage = 2;

        /// 이 거리 안이면 쓸 수 있다.
        [SerializeField] float _range = 1.2f;

        /// 이 거리보다 가까우면 못 쓴다(원거리 공격이 코앞에서 발사되지 않게). 0이면 제한 없음.
        [SerializeField] float _minRange;

        /// 같은 공격을 다시 쓰기까지의 시간.
        [SerializeField] float _cooldown = 1.2f;

        /// ⚠️ 내지르기 전에 멈춰 있는 시간. 0이면 예고 없이 즉발이고, 지금 슬라임들이 그렇다.
        /// 기획에 '공격 예고'가 실제로 있는지 확인되지 않아 기본값을 0으로 둔다 — 값만 넣으면
        /// 타이밍이 생기고, 보여주는 일은 MonsterAI.Telegraphing을 듣는 쪽이 맡는다.
        [SerializeField] float _windup;

        /// 방패로 막히는 공격인지. 방어불가를 실제로 쓰는 공격이 여기서 처음 생긴다
        /// (판정 쪽 이음새는 PlayerHealth에 이미 있었다).
        [SerializeField] DamageKind _kind = DamageKind.Normal;

        public string DisplayName => string.IsNullOrEmpty(_displayName) ? name : _displayName;
        public int Damage => Mathf.Max(0, _damage);
        public float Range => Mathf.Max(0.1f, _range);
        public float MinRange => Mathf.Max(0f, _minRange);
        public float Cooldown => Mathf.Max(0.05f, _cooldown);
        public float Windup => Mathf.Max(0f, _windup);
        public DamageKind Kind => _kind;

        /// 지금 이 거리에서 쓸 수 있는지. 사거리 말고 다른 조건이 생기면 여기를 덮는다.
        public virtual bool CanUse(in MonsterAttackContext context)
            => context.Distance <= Range && context.Distance >= MinRange;

        /// 윈드업이 끝나고 실제로 내지르는 순간. 여기서 피해를 주거나 투사체를 띄운다.
        public abstract void Execute(in MonsterAttackContext context);

        public virtual string Describe()
            => $"{DisplayName} · 피해 {Damage} · 사거리 {Range:0.#} · {Cooldown:0.#}초"
               + (Windup > 0f ? $" · 예고 {Windup:0.##}초" : string.Empty)
               + (_kind == DamageKind.Unblockable ? " · 방어불가" : string.Empty);
    }
}
