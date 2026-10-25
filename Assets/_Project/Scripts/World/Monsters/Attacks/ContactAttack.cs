using UnityEngine;

namespace ForagerCP
{
    /// 몸통에 닿으면 들어가는 피해. 돌무더기·광갑 골렘이 전제로 깔고 있다.
    ///
    /// 물리 충돌(OnCollisionStay)로 만들지 않고 공격 하나로 둔 이유가 둘 있다.
    /// ① 수치·방패 판정·재사용 대기를 다른 공격과 같은 자리에서 읽게 된다.
    /// ② 빈틈 구간에는 닿아도 안 들어간다 — 빈틈은 플레이어의 반격 창인데
    ///    거기서 몸통 피해가 계속 들어오면 붙어서 때릴 수가 없다.
    /// 사거리를 몸 반경쯤으로 두고 목록 맨 아래에 놓으면, 제 공격이 대기 중일 때만 닿아서 아프다.
    ///
    /// 몬스터 공격 중 유일하게 여러 몹이 같이 쓰는 클래스다. 나머지는 몹별로 하나씩 둔다.
    [CreateAssetMenu(menuName = "ForagerCP/몬스터 공격/접촉", fileName = "Attack_Contact")]
    public class ContactAttack : MonsterAttack
    {
        public override void Execute(in MonsterAttackContext context)
        {
            if (context.Target == null || !context.Target.IsAlive) return;
            context.Target.TakeDamage(Damage, context.Self.gameObject, Kind);
        }

        public override string Describe() => base.Describe() + " · 접촉";
    }
}
