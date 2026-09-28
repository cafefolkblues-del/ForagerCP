using System;
using UnityEngine;

namespace ForagerCP
{
    /// 우클릭을 누르고 있는 동안 정면 180°의 피해를 막는다.
    ///
    /// 확정 스펙: 막으면 데미지 0 + 넉백도 없음(완전 차단). 방패 중에는 저속 이동만 되고
    /// 공격·대시는 막힌다 — 그 판단은 PlayerActionState가 한다.
    /// 등 뒤나 옆에서 들어온 공격은 그대로 맞는다.
    public class PlayerShield : MonoBehaviour
    {
        [SerializeField] GameInput _input;
        [SerializeField] PlayerActionState _state;

        /// 정면 180° = 바라보는 방향 기준 좌우 90°.
        [SerializeField, Range(0f, 180f)] float _blockHalfAngle = 90f;

        public event Action<PlayerShield> Blocked;
        public event Action<bool> ShieldingChanged;

        public bool IsShielding { get; private set; }

        void Awake()
        {
            if (_input == null) _input = GetComponent<GameInput>();
            if (_state == null) _state = GetComponent<PlayerActionState>();
        }

        void Update()
        {
            bool wanted = _input.ShieldHeld && (_state == null || _state.CanShield);
            if (wanted == IsShielding) return;

            IsShielding = wanted;
            ShieldingChanged?.Invoke(IsShielding);
        }

        /// 그 위치에서 들어온 공격을 막아내는가. 각도 판정은 여기 한 곳에서만 한다.
        public bool Blocks(Vector3 sourcePosition)
        {
            if (!IsShielding) return false;

            Vector3 toSource = sourcePosition - transform.position;
            toSource.y = 0f;
            if (toSource.sqrMagnitude < 0.0001f) return true; // 겹쳐 있으면 막은 것으로 본다

            bool blocked = Vector3.Angle(transform.forward, toSource) <= _blockHalfAngle;
            if (blocked) Blocked?.Invoke(this);
            return blocked;
        }

        void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying) return;

            Gizmos.color = IsShielding ? new Color(0.3f, 0.7f, 1f, 0.9f) : new Color(0.3f, 0.7f, 1f, 0.25f);
            Gizmos.DrawRay(transform.position, Quaternion.Euler(0f, -_blockHalfAngle, 0f) * transform.forward * 2f);
            Gizmos.DrawRay(transform.position, Quaternion.Euler(0f, _blockHalfAngle, 0f) * transform.forward * 2f);
        }
    }
}
