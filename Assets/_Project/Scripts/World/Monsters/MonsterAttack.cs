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

        /// 내지른 뒤 아무것도 못 하는 시간. 재사용 대기(Cooldown)와 다른 축이다 —
        /// 대기는 '이 공격을 다시 쓰기까지'이고, 빈틈은 '이동도 다른 공격도 못 하는' 구간이다.
        /// 기획서가 몬스터마다 예고와 빈틈을 따로 적어둔 이유이고,
        /// 1구역의 "막거나 피하면 잠시 멈추니 반격할 수 있다"가 이 값으로 성립한다.
        [SerializeField] float _recovery;

        /// 방패에 막혔을 때 추가로 멈추는 시간. 0이면 빈틈과 같게 둔다 —
        /// 막아낸 보람이 빈틈보다 짧으면 방패를 들 이유가 없어진다.
        [SerializeField] float _staggerWhenBlocked;

        /// 방패로 막히는 공격인지. 방어불가를 실제로 쓰는 공격이 여기서 처음 생긴다
        /// (판정 쪽 이음새는 PlayerHealth에 이미 있었다).
        [SerializeField] DamageKind _kind = DamageKind.Normal;

        public string DisplayName => string.IsNullOrEmpty(_displayName) ? name : _displayName;
        public int Damage => Mathf.Max(0, _damage);
        public float Range => Mathf.Max(0.1f, _range);
        public float MinRange => Mathf.Max(0f, _minRange);
        public float Cooldown => Mathf.Max(0.05f, _cooldown);
        public float Windup => Mathf.Max(0f, _windup);
        public float Recovery => Mathf.Max(0f, _recovery);
        public float StaggerWhenBlocked => _staggerWhenBlocked > 0f ? _staggerWhenBlocked : Recovery;
        public DamageKind Kind => _kind;

        /// 지금 이 거리에서 쓸 수 있는지. 사거리 말고 다른 조건이 생기면 여기를 덮는다.
        public virtual bool CanUse(in MonsterAttackContext context)
            => context.Distance <= Range && context.Distance >= MinRange;

        /// 윈드업이 끝나고 실제로 내지르는 순간. 여기서 피해를 주거나 투사체를 띄운다.
        public abstract void Execute(in MonsterAttackContext context);

        /// 몸을 쓰는 공격(돌진·도약 등)인가. 참이면 내지른 뒤 실행기의 AttackMotion 칸에서
        /// 끝날 때까지 TickMotion이 매 프레임 불리고, 그다음 빈틈으로 간다.
        /// 지금은 이 자리만 있고 쓰는 공격이 없다(돌진 구현은 패턴이 정해질 때까지 폐기).
        public virtual bool HasMotion => false;

        /// 몸을 쓰는 공격의 한 프레임. 아직 진행 중이면 true.
        ///
        /// 몸은 ctx.Motor로만 옮기고, 진행 값(방향·남은 거리·맞혔는지)은 ctx.Memory에 적는다 —
        /// 공격 에셋은 공유 자산이라 값을 들 수 없다. 맞거나 막히면 실행기가 먼저 끊으므로 여기서 묻지 않는다.
        public virtual bool TickMotion(MonsterContext ctx) => false;

        public virtual string Describe()
            => $"{DisplayName} · 피해 {Damage} · 사거리 {Range:0.#} · {Cooldown:0.#}초"
               + (Windup > 0f ? $" · 예고 {Windup:0.##}초" : string.Empty)
               + (Recovery > 0f ? $" · 빈틈 {Recovery:0.##}초" : string.Empty)
               + (_kind == DamageKind.Unblockable ? " · 방어불가" : string.Empty);
    }
}
