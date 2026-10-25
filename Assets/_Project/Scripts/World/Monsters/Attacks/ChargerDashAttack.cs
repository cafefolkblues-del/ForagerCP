using UnityEngine;

namespace ForagerCP
{
    /// 예고 뒤 직선으로 들이받는다. 3구역 광석 돌진수가 쓴다.
    ///
    /// 이동을 여기서 직접 하지 않고 MonsterAI에 부탁하는 이유: 위치를 쥐는 곳이 둘이 되면
    /// 넉백·경직과 누가 몸을 옮기는지가 흐려진다. 공격은 "이 방향으로 이만큼"만 말한다.
    [CreateAssetMenu(menuName = "ForagerCP/몬스터 공격/돌진", fileName = "Attack_Lunge")]
    public class LungeAttack : MonsterAttack
    {
        [Header("돌진")]
        [SerializeField] float _speed = 10f;

        /// 예고가 끝난 지점에서 이만큼 나아간다. 사거리와 따로 두는 이유:
        /// 사거리는 "언제 시작하는가"이고 이쪽은 "얼마나 가는가"다.
        [SerializeField] float _distance = 5f;

        /// 몸이 스치는 반경. 돌진은 지나가며 한 번만 맞힌다.
        [SerializeField] float _hitRadius = 0.9f;

        public override void Execute(in MonsterAttackContext context)
        {
            if (context.Brain == null || context.TargetTransform == null) return;

            // 예고가 끝난 순간의 방향으로 굳힌다. 달리면서 꺾으면 옆으로 피할 수가 없다 —
            // 기획이 "중간에 방향을 바꾸지 않는다"로 못박은 지점이다.
            Vector3 aim = context.TargetTransform.position - context.Self.position;
            aim.y = 0f;
            if (aim.sqrMagnitude < 0.0001f) aim = context.Self.forward;

            context.Brain.BeginLunge(aim.normalized, _speed, _distance, Damage, Kind, _hitRadius);
        }

        public override string Describe()
            => base.Describe() + $" · 돌진 {_distance:0.#}m";
    }
}
