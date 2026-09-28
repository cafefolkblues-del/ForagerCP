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
        Bounds _bodyBounds;
        Vector3 _centerOffset;
        bool _hasBounds;
        Vector3 _velocity;
        float _endTime;
        float _lockedY;

        /// 이동 컴포넌트가 이 값을 보고 자기 이동을 건너뛴다.
        public bool IsActive => Time.time < _endTime;

        void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _ownColliders = GetComponentsInChildren<Collider>(true);

            // 콜라이더가 꺼진 뒤에는 bounds를 믿을 수 없어 켜져 있는 지금 크기를 기억해둔다.
            for (int i = 0; i < _ownColliders.Length; i++)
            {
                if (_ownColliders[i] == null) continue;
                if (!_hasBounds) { _bodyBounds = _ownColliders[i].bounds; _hasBounds = true; }
                else _bodyBounds.Encapsulate(_ownColliders[i].bounds);
            }

            // 리지드바디 위치가 곧 몸통 중심은 아니다(피벗이 발밑인 모델도 있다).
            // 중심까지의 차이를 기억해 캐스트를 항상 몸통 한가운데에서 쏜다.
            if (_hasBounds) _centerOffset = _bodyBounds.center - transform.position;
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

            // 발밑이 아니라 몸통 높이에서 쏜다. 바닥에 붙어 있는 몬스터는 발밑에서 쏘면
            // 지면이 먼저 걸려 모든 방향이 막힌 것으로 판정된다(플레이어는 키가 커서 안 걸렸다).
            float halfHeight = _hasBounds ? _bodyBounds.extents.y : 0.5f;
            float radius = Mathf.Min(_blockCheckRadius, Mathf.Max(0.05f, halfHeight * 0.8f));
            Vector3 direction = step / distance;

            // 구를 반지름만큼 뒤로 물려서 쏜다. 벽에 바짝 붙은 상태로 그 자리에서 쏘면
            // 시작부터 겹쳐 거리 0으로 잡히고, 법선이 진행 반대방향으로 나와 벽인지 알 수 없다.
            Vector3 origin = _body.position + _centerOffset - direction * radius;
            float castDistance = distance + radius;

            var hits = Physics.SphereCastAll(origin, radius, direction,
                castDistance, ~0, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hits.Length; i++)
            {
                Collider hit = hits[i].collider;
                if (hit == null || IsOwn(hit)) continue;

                // 시작부터 겹쳐 있으면 법선이 진행 반대방향으로 나와 어느 쪽으로도 막힌 것처럼 보인다.
                // 이미 낀 상태는 높이 고정만으로 충분하니 여기서는 세지 않는다.
                if (hits[i].distance <= 0.0001f) continue;

                // 위(바닥·경사면)나 아래(천장)를 향한 면은 벽이 아니다. 수평으로 막는 것만 본다.
                if (Mathf.Abs(hits[i].normal.y) > 0.7f) continue;

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
