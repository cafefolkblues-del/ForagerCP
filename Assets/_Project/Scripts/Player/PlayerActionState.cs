using UnityEngine;

namespace ForagerCP
{
    /// 플레이어 행동들의 소유권 중재자.
    ///
    /// 확정 우선순위: 넉백 > 경직 > 대시 > 방패 > 이동.
    /// 이 판단을 각 컴포넌트가 따로 하면 "대시 중 방패 금지", "방패 중 공격 금지" 같은 규칙이
    /// 다섯 군데로 흩어져 서로 어긋난다. 규칙은 여기에만 둔다.
    ///
    /// 각 행동 컴포넌트는 자기 상태(IsDashing 등)만 공개하고, 허용 여부는 이 클래스에 물어본다.
    /// 반대로 이 클래스는 남의 상태를 읽기만 해서 호출이 순환하지 않는다.
    public class PlayerActionState : MonoBehaviour
    {
        [SerializeField] Knockback _knockback;
        [SerializeField] PlayerDash _dash;
        [SerializeField] PlayerShield _shield;
        [SerializeField] PlayerAttacker _attacker;

        [Header("이동 배율")]
        [SerializeField] float _shieldMoveMultiplier = 0.45f;
        [SerializeField] float _chargeMoveMultiplier = 0.45f;

        void Awake()
        {
            if (_knockback == null) _knockback = GetComponent<Knockback>();
            if (_dash == null) _dash = GetComponent<PlayerDash>();
            if (_shield == null) _shield = GetComponent<PlayerShield>();
            if (_attacker == null) _attacker = GetComponent<PlayerAttacker>();
        }

        public bool IsKnockback => _knockback != null && _knockback.IsActive;
        public bool IsDashing => _dash != null && _dash.IsDashing;
        public bool IsShielding => _shield != null && _shield.IsShielding;
        public bool IsCharging => _attacker != null && _attacker.IsCharging;

        /// 넉백은 위치를 직접 움직이므로 이동 컴포넌트가 손을 떼야 한다.
        /// 대시는 '순간이동'이 아니라 이속 부스트라 이동 소유권은 그대로 둔다.
        public bool MovementOwnedByOther => IsKnockback;

        public float MoveSpeedMultiplier
        {
            get
            {
                if (IsDashing) return _dash.SpeedMultiplier;
                if (IsShielding) return _shieldMoveMultiplier;
                if (IsCharging) return _chargeMoveMultiplier;
                return 1f;
            }
        }

        /// 방패를 든 채로는 못 때린다(확정 스펙). 넉백 중에도 마찬가지.
        public bool CanAttack => !IsKnockback && !IsShielding;

        /// 대시 중에는 방어할 수 없다(확정 스펙).
        public bool CanShield => !IsKnockback && !IsDashing;

        /// 방패를 든 채로는 대시할 수 없다. 우클릭을 떼면 바로 가능.
        public bool CanDash => !IsKnockback && !IsShielding;
    }
}
