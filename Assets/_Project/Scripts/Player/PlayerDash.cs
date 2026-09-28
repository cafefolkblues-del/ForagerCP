using System;
using UnityEngine;

namespace ForagerCP
{
    /// 대시 = 짧은 이속 부스트. 순간이동이 아니라서 이동 소유권은 PlayerMotor가 계속 쥐고,
    /// 여기는 '지금 대시 중인가 / 배율이 얼마인가'만 알려준다.
    ///
    /// 확정 스펙: i-frame은 기본 꺼둔다(_invulnerableTime = 0). 훅만 뚫어두고,
    /// 무적 대시로 갈지는 기획이 정하면 이 값만 올리면 된다.
    public class PlayerDash : MonoBehaviour
    {
        [SerializeField] GameInput _input;
        [SerializeField] PlayerActionState _state;

        [SerializeField] float _speedMultiplier = 2.2f;
        [SerializeField] float _duration = 0.18f;
        [SerializeField] float _cooldown = 1.2f;

        /// 0이면 무적 없음. 기획이 무적 대시를 원하면 이 값만 올린다.
        [SerializeField] float _invulnerableTime = 0f;

        public event Action<PlayerDash> Dashed;

        public bool IsDashing => Time.time < _dashEndTime;
        public bool IsInvulnerable => Time.time < _invulnerableUntil;
        public float SpeedMultiplier => Mathf.Max(1f, _speedMultiplier);
        public float CooldownRatio => _cooldown <= 0f ? 0f : Mathf.Clamp01((_readyTime - Time.time) / _cooldown);

        float _dashEndTime;
        float _readyTime;
        float _invulnerableUntil;

        void Awake()
        {
            if (_input == null) _input = GetComponent<GameInput>();
            if (_state == null) _state = GetComponent<PlayerActionState>();
        }

        void OnEnable() => _input.DashPressed += TryDash;

        void OnDisable() => _input.DashPressed -= TryDash;

        void TryDash()
        {
            if (Time.time < _readyTime) return;
            if (_state != null && !_state.CanDash) return;

            _dashEndTime = Time.time + _duration;
            _readyTime = Time.time + _cooldown;
            if (_invulnerableTime > 0f) _invulnerableUntil = Time.time + _invulnerableTime;

            Dashed?.Invoke(this);
        }
    }
}
