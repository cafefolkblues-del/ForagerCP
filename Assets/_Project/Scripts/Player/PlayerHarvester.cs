using UnityEngine;

namespace ForagerCP
{
    /// 좌클릭 채집. 전방 부채꼴 안의 대상 중 최근접 하나만 때린다(사양 1-2).
    public class PlayerHarvester : MonoBehaviour
    {
        [SerializeField] GameInput _input;
        [SerializeField] HarvestPower _power;
        [SerializeField] float _range = 2.5f;
        [SerializeField, Range(0f, 180f)] float _halfAngle = 45f;
        [SerializeField] float _cooldown = 0.4f;

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

            IHarvestable target = FindNearestTarget();
            if (target == null) return; // 헛스윙은 쿨을 소모시키지 않는다

            _nextSwingTime = Time.time + _cooldown;
            target.Harvest(_power.Value);
        }

        IHarvestable FindNearestTarget()
        {
            // OverlapSphere + 컴포넌트 필터: 레이어/태그 문자열에 의존하지 않아
            // 레이어 설정이 깨져도 판정이 죽지 않는다.
            Collider[] hits = Physics.OverlapSphere(transform.position, _range);

            IHarvestable best = null;
            float bestSqrDistance = float.MaxValue;
            Vector3 facing = transform.forward;

            for (int i = 0; i < hits.Length; i++)
            {
                IHarvestable candidate = hits[i].GetComponentInParent<IHarvestable>();
                if (candidate == null || !candidate.IsHarvestable) continue;

                Vector3 toTarget = candidate.Transform.position - transform.position;
                toTarget.y = 0f;
                if (Vector3.Angle(facing, toTarget) > _halfAngle) continue;

                float sqrDistance = toTarget.sqrMagnitude;
                if (sqrDistance >= bestSqrDistance) continue;

                bestSqrDistance = sqrDistance;
                best = candidate;
            }

            return best;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _range);
        }
    }
}
