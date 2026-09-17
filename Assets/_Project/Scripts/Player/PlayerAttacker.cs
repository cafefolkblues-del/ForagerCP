using UnityEngine;

namespace ForagerCP
{
    /// 좌클릭 하나로 채집과 전투를 겸한다(사양 2-1: 대상이 몬스터면 기본 공격으로 전환).
    /// 대상 탐색·쿨다운은 공유하고, 실제 처리만 대상 종류에 따라 갈린다.
    public class PlayerAttacker : MonoBehaviour
    {
        [SerializeField] GameInput _input;
        [SerializeField] HarvestPower _power;
        [SerializeField] float _range = 2.5f;
        [SerializeField, Range(0f, 180f)] float _halfAngle = 45f;
        [SerializeField] float _cooldown = 0.4f;

        /// 전투 공격력. 채집력과 분리해둔다 — 곡괭이 강화가 전투력까지 올리는지는 기획 미확정.
        [SerializeField] int _attackPower = 5;

        float _nextSwingTime;

        void Awake()
        {
            if (_input == null) _input = GetComponent<GameInput>();
            if (_power == null) _power = GetComponent<HarvestPower>();
        }

        void Update()
        {
            if (!_input.AttackHeld) return;
            if (Time.time < _nextSwingTime) return;

            Component target = FindNearestTarget();
            if (target == null) return; // 헛스윙은 쿨을 소모시키지 않는다

            _nextSwingTime = Time.time + _cooldown;

            if (target is IDamageable damageable) damageable.TakeDamage(_attackPower, gameObject);
            else if (target is IHarvestable harvestable) harvestable.Harvest(_power.Value);
        }

        /// 전방 부채꼴 안에서 가장 가까운 대상 하나.
        /// 몬스터와 광물을 따로 훑지 않고 한 번에 고른다 — 눈앞의 것을 치는 게 자연스럽기 때문.
        Component FindNearestTarget()
        {
            // OverlapSphere + 컴포넌트 필터: 레이어/태그 문자열에 의존하지 않아
            // 레이어 설정이 깨져도 판정이 죽지 않는다.
            Collider[] hits = Physics.OverlapSphere(transform.position, _range);

            Component best = null;
            float bestSqrDistance = float.MaxValue;
            Vector3 facing = transform.forward;

            for (int i = 0; i < hits.Length; i++)
            {
                Component candidate = PickTarget(hits[i]);
                if (candidate == null) continue;

                Vector3 toTarget = candidate.transform.position - transform.position;
                toTarget.y = 0f;
                if (Vector3.Angle(facing, toTarget) > _halfAngle) continue;

                float sqrDistance = toTarget.sqrMagnitude;
                if (sqrDistance >= bestSqrDistance) continue;

                bestSqrDistance = sqrDistance;
                best = candidate;
            }

            return best;
        }

        Component PickTarget(Collider hit)
        {
            var damageable = hit.GetComponentInParent<IDamageable>();

            // 자기 자신(플레이어 체력)은 대상에서 뺀다.
            if (damageable != null && damageable.IsAlive && damageable.Transform != transform)
                return damageable as Component;

            var harvestable = hit.GetComponentInParent<IHarvestable>();
            if (harvestable != null && harvestable.IsHarvestable) return harvestable as Component;

            return null;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _range);
        }
    }
}
