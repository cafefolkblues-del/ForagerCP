using UnityEngine;

namespace ForagerCP
{
    /// 광석 돌진수의 공격. 예고 때 정한 방향으로 돌진하고 한 번 타격한다.
    ///
    /// HasMotion을 통해 TickMotion에서 이동과 타격을 처리한다.
    /// 몸은 ctx.Motor로 움직이고, 진행 값은 ctx.Memory에 적는다(R3~R5가 공격 에셋을 각각 사용한다).
    [CreateAssetMenu(menuName = "ForagerCP/몬스터 공격/돌진수 돌진", fileName = "Attack_Charger_Dash")]
    public class ChargerDashAttack : MonsterAttack
    {
        [SerializeField] private float _speed = 11f;
        [SerializeField] private float _distance = 5f;
        [SerializeField] private float _hitRadius = 0.9f;

        public override bool HasMotion => true;

        public override void Execute(in MonsterAttackContext context) { }

        public override bool TickMotion(MonsterContext ctx)
        {
            var memory = ctx.Memory;

            // 전용 예고가 저장한 방향을 사용한다. 예고가 없을 때만 여기서 방향을 정한다.
            if (!memory.DashInitialized)
            {
                if (!ctx.Aggro.HasTarget)
                    return false;

                Vector3 direction = ctx.ToTarget;
                direction.y = 0f;

                if (direction.sqrMagnitude < 0.0001f)
                    return false;

                memory.DashDirection = direction.normalized;
                memory.DashTravelled = 0f;
                memory.DashHasHit = false;
                memory.DashInitialized = true;

                ctx.Motor.Face(memory.DashDirection);
            }

            float remaining = _distance - memory.DashTravelled;
            if (remaining <= 0f || _speed <= 0f)
                return false;

            float step = Mathf.Min(
                _speed * Time.fixedDeltaTime,
                remaining
            );

            float allowedStep = ctx.Motor.GetAllowedStep(
                memory.DashDirection,
                step,
                ctx.Aggro.Target
            );
            bool blocked = allowedStep < step;

            if (allowedStep <= 0f)
                return false;

            // 이동할 수 있는 구간에 대해서만 타격을 검사한다.
            if (!ctx.Motor.CanStepWithin(
                memory.DashDirection,
                allowedStep / Time.fixedDeltaTime,
                ctx.Aggro.HomeRegion))
            {
                return false;
            }

            TryHitTarget(ctx, allowedStep);

            // 피해 처리 중 방패 방어·전투 초기화로 돌진이 취소될 수 있다.
            if (memory.Motion != this)
                return false;

            bool moved = ctx.Motor.TryStepWithin(
                memory.DashDirection,
                allowedStep / Time.fixedDeltaTime,
                ctx.Aggro.HomeRegion
            );

            if (!moved)
                return false;

            memory.DashTravelled += allowedStep;

            return !blocked && memory.DashTravelled < _distance;
        }

        private void TryHitTarget(MonsterContext ctx, float step)
        {
            if (ctx.Memory.DashHasHit || !ctx.Aggro.HasTarget)
                return;

            IDamageable target = ctx.Aggro.TargetDamageable;
            if (target == null || !target.IsAlive || _hitRadius <= 0f)
                return;

            Vector3 start = ctx.Motor.Position;
            Vector3 end = start + ctx.Memory.DashDirection * step;

            // 이번 물리 프레임에 지나갈 구간을 통째로 검사한다.
            Collider[] colliders = Physics.OverlapCapsule(
                start,
                end,
                _hitRadius,
                ~0,
                QueryTriggerInteraction.Ignore
            );

            foreach (Collider collider in colliders)
            {
                if (!collider.transform.IsChildOf(ctx.Aggro.Target))
                    continue;

                // 타격 범위가 벽 너머까지 닿아도 피해를 주지 않는다.
                Vector3 toTarget = collider.bounds.center - start;

                if (toTarget.sqrMagnitude > 0.0001f)
                {
                    RaycastHit[] obstacles = Physics.RaycastAll(
                        start,
                        toTarget.normalized,
                        toTarget.magnitude,
                        ~0,
                        QueryTriggerInteraction.Ignore
                    );

                    foreach (RaycastHit obstacle in obstacles)
                    {
                        Transform obstacleTransform = obstacle.collider.transform;

                        if (obstacleTransform.IsChildOf(ctx.Monster.transform))
                            continue;

                        if (obstacleTransform.IsChildOf(ctx.Aggro.Target))
                            continue;

                        return;
                    }
                }

                // 피해 처리보다 먼저 표시해서 중복 타격을 막는다.
                ctx.Memory.DashHasHit = true;
                target.TakeDamage(Damage, ctx.Monster.gameObject, Kind);
                return;
            }
        }

        public override string Describe() => base.Describe() + " · 돌진";
    }
}
