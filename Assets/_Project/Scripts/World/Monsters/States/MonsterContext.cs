using UnityEngine;

namespace ForagerCP
{
    /// 상태가 일할 때 받는 것 전부. 몬스터 한 마리당 하나이고 실행기가 매 프레임 거리를 새로 적는다.
    ///
    /// 공격 고르기·내지르기도 여기 둔다. 쿨다운을 "공격마다 따로, 내지른 순간부터" 세는 규칙은
    /// 확정이라 몹별 상태가 각자 세면 안 된다. 상태는 고르고(PickAttack) 시작하라고(StartAttack)만 한다.
    public class MonsterContext
    {
        public readonly MonsterAI Brain;
        public readonly Monster Monster;
        public readonly MonsterDefinition Definition;
        public readonly MonsterMemory Memory;
        public readonly MonsterMotor Motor;
        public readonly MonsterAggro Aggro;

        /// 표적까지 평면 벡터와 거리, 집까지 평면 벡터. 프레임 시작에 한 번 잰다.
        public Vector3 ToTarget { get; private set; }
        public float Distance { get; private set; }
        public Vector3 ToHome { get; private set; }

        /// 상태는 Time.time을 직접 부르지 않는다 — 창 열면 시간 정지(TODO 28)를 넣을 때 여기 하나만 바꾼다.
        public float Now => Time.time;

        public MonsterContext(MonsterAI brain, Monster monster, MonsterDefinition definition,
                              MonsterMemory memory, MonsterMotor motor, MonsterAggro aggro)
        {
            Brain = brain;
            Monster = monster;
            Definition = definition;
            Memory = memory;
            Motor = motor;
            Aggro = aggro;
        }

        public void Measure(Vector3 from)
        {
            ToTarget = Aggro.ToTarget(from);
            Distance = ToTarget.magnitude;
            ToHome = Aggro.ToHome(from);
        }

        /// 이 몹의 후퇴 칸. 비어 있으면 null — 후퇴하지 않는 몹이다.
        public RetreatState Retreat => Brain.Resolve(StateId.Retreat) as RetreatState;

        /// 쓸 수 있는 것 중 목록에서 앞선 것. 우선순위를 코드가 아니라 에셋 순서가 정한다. 없으면 -1.
        public int PickAttack()
        {
            IDamageable target = Aggro.TargetDamageable;
            if (target == null || !target.IsAlive) return -1;

            var attackContext = new MonsterAttackContext(Monster, target, Aggro.Target, Distance);

            for (int i = 0; i < Definition.Attacks.Count; i++)
            {
                MonsterAttack attack = Definition.Attacks[i];
                if (attack == null) continue;
                if (Now < Memory.ReadyAt[i]) continue;
                if (!attack.CanUse(attackContext)) continue;

                return i;
            }
            return -1;
        }

        /// 공격을 시작한다. 예고가 있으면 Windup으로, 없으면 그 자리에서 내지르고 다음 칸을 돌려준다.
        public StateId StartAttack(int index)
        {
            MonsterAttack attack = Definition.Attacks[index];
            if (attack.Windup <= 0f)
            {
                Fire(index);
                return AfterFire();
            }

            Memory.WindingIndex = index;
            Memory.WindupEndsAt = Now + attack.Windup;
            Memory.Winding = attack;
            return StateId.Windup;
        }

        public void Fire(int index)
        {
            MonsterAttack attack = Definition.Attacks[index];

            // 재사용 대기는 내지른 때부터 센다. 예고 시작부터 세면 예고가 긴 공격이
            // 사실상 더 자주 나간다.
            Memory.ReadyAt[index] = Now + attack.Cooldown;
            Memory.RecoverUntil = Now + attack.Recovery;
            Memory.LastFired = attack;
            Memory.CancelWindup();

            // 몸을 쓰는 공격이면 내지른 뒤 AttackMotion 칸이 TickMotion을 돌린다.
            Memory.Motion = attack.HasMotion ? attack : null;
            Memory.MotionStartedAt = Now;

            attack.Execute(new MonsterAttackContext(Monster, Aggro.TargetDamageable, Aggro.Target, Distance));
        }

        /// 내지른 뒤 어디로 가나. 몸을 쓰는 공격이면 그 동작이 먼저, 그다음 빈틈.
        public StateId AfterFire()
        {
            if (Memory.InMotion) return StateId.AttackMotion;
            return Now < Memory.RecoverUntil ? StateId.Recover : StateId.Chase;
        }
    }
}
