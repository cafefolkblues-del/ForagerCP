using UnityEngine;

namespace ForagerCP
{
    /// 맞은 쪽을 반대 방향으로 잠깐 밀어내는 연출.
    /// 이동 컴포넌트(PlayerMotor / MonsterAI)가 매 FixedUpdate마다 MovePosition으로 위치를 덮어쓰기 때문에,
    /// 힘(AddForce)을 주는 방식으로는 밀리지 않는다. 그래서 밀리는 동안 이동을 양보받는 구조로 간다.
    [RequireComponent(typeof(Rigidbody))]
    public class Knockback : MonoBehaviour
    {
        [SerializeField] float _force = 5f;
        [SerializeField] float _duration = 0.16f;

        /// 밀리는 경로에 벽이 있는지 볼 때 쓰는 반지름. 0이면 검사하지 않는다.
        [SerializeField] float _blockCheckRadius = 0.35f;

        Rigidbody _body;
        Collider[] _ownColliders;
        Vector3 _velocity;
        float _endTime;
        float _lockedY;

        /// 이동 컴포넌트가 이 값을 보고 자기 이동을 건너뛴다.
        public bool IsActive => Time.time < _endTime;

        void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _ownColliders = GetComponentsInChildren<Collider>(true);
        }

        public void Apply(Vector3 direction, float forceOverride = -1f)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) direction = -transform.forward;

            _velocity = direction.normalized * (forceOverride > 0f ? forceOverride : _force);
            _endTime = Time.time + _duration;

            // 🐛 밀린 대상이 구조물 사이에 끼면 물리가 겹침을 밀어내며 위로 솟구쳤다(팀 리뷰: "뒤에 오브젝트가 있으면 날아감").
            // 밀리는 동안은 높이를 시작 시점으로 묶어 수평으로만 밀리게 한다.
            _lockedY = _body.position.y;
        }

        /// 때린 쪽 위치를 주면 반대 방향으로 민다.
        public void ApplyFrom(Transform source, float forceOverride = -1f)
        {
            if (source == null) return;
            Apply(transform.position - source.position, forceOverride);
        }

        /// 벽에 부딪혔으면 남은 밀림을 버린다. 계속 밀면 콜라이더를 파고들며 튕겨 나간다.
        public void Stop() => _endTime = 0f;

        void FixedUpdate()
        {
            if (!IsActive) return;

            // 남은 시간에 비례해 감속 — 끝날 때 뚝 멈추지 않게.
            float remaining = Mathf.Clamp01((_endTime - Time.time) / _duration);
            Vector3 step = _velocity * (remaining * Time.fixedDeltaTime);

            if (IsBlocked(step))
            {
                Stop();
                return;
            }

            Vector3 target = _body.position + step;
            target.y = _lockedY;
            _body.MovePosition(target);
        }

        bool IsBlocked(Vector3 step)
        {
            if (_blockCheckRadius <= 0f) return false;

            float distance = step.magnitude;
            if (distance < 0.0001f) return false;

            var hits = Physics.SphereCastAll(_body.position, _blockCheckRadius, step / distance,
                distance, ~0, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hits.Length; i++)
            {
                Collider hit = hits[i].collider;
                if (hit == null || IsOwn(hit)) continue;
                return true;
            }

            return false;
        }

        bool IsOwn(Collider collider)
        {
            for (int i = 0; i < _ownColliders.Length; i++)
            {
                if (_ownColliders[i] == collider) return true;
            }
            return false;
        }
    }
}
