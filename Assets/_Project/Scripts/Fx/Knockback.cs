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

        Rigidbody _body;
        Vector3 _velocity;
        float _endTime;

        /// 이동 컴포넌트가 이 값을 보고 자기 이동을 건너뛴다.
        public bool IsActive => Time.time < _endTime;

        void Awake() => _body = GetComponent<Rigidbody>();

        public void Apply(Vector3 direction, float forceOverride = -1f)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) direction = -transform.forward;

            _velocity = direction.normalized * (forceOverride > 0f ? forceOverride : _force);
            _endTime = Time.time + _duration;
        }

        /// 때린 쪽 위치를 주면 반대 방향으로 민다.
        public void ApplyFrom(Transform source, float forceOverride = -1f)
        {
            if (source == null) return;
            Apply(transform.position - source.position, forceOverride);
        }

        void FixedUpdate()
        {
            if (!IsActive) return;

            // 남은 시간에 비례해 감속 — 끝날 때 뚝 멈추지 않게.
            float remaining = Mathf.Clamp01((_endTime - Time.time) / _duration);
            _body.MovePosition(_body.position + _velocity * (remaining * Time.fixedDeltaTime));
        }
    }
}
