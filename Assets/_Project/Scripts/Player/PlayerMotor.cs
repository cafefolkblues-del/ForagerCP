using UnityEngine;

namespace ForagerCP
{
    /// WASD 월드 이동 + 마우스 커서 방향 조준(3D 탑다운).
    /// 조준 방향은 채집 전방 판정의 기준이라 PlayerHarvester가 transform.forward로 읽어간다.
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerMotor : MonoBehaviour
    {
        [SerializeField] GameInput _input;
        [SerializeField] Camera _camera;
        [SerializeField] Knockback _knockback;
        [SerializeField] PlayerHealth _health;
        [SerializeField] PlayerActionState _state;

        /// 바라보는 방향을 이 각도 단위로 스냅한다. 45 = 8방향(확정 스펙). 0이면 자유 조준.
        [SerializeField] float _facingSnapDegrees = 45f;

        /// 이 높이 아래로 내려가면 맵을 뚫은 것으로 본다.
        [SerializeField] float _fallSafeY = -3f;
        [SerializeField] float _moveSpeed = 5f;

        Rigidbody _body;

        void Awake()
        {
            _body = GetComponent<Rigidbody>();
            if (_input == null) _input = GetComponent<GameInput>();
            if (_camera == null) _camera = Camera.main;
            if (_knockback == null) _knockback = GetComponent<Knockback>();
            if (_health == null) _health = GetComponent<PlayerHealth>();
            if (_state == null) _state = GetComponent<PlayerActionState>();

            // 조준 회전을 직접 세팅하므로 물리 회전은 막는다(충돌로 캐릭터가 돌아가는 것 방지).
            _body.freezeRotation = true;
        }

        void Update() => AimAtPointer();

        void FixedUpdate()
        {
            // 몬스터와 같은 이유의 구제 — 바닥 아래로 빠지면 스스로는 못 돌아온다.
            if (transform.position.y < _fallSafeY && _health != null)
            {
                _health.Respawn();
                return;
            }

            // 넉백은 위치를 직접 옮기므로 이동이 손을 뗀다. 안 그러면 밀림이 걸음으로 상쇄된다.
            // 대시·방패는 소유권을 뺏지 않고 배율만 바꾼다(PlayerActionState가 판단).
            if (_state != null ? _state.MovementOwnedByOther : (_knockback != null && _knockback.IsActive)) return;

            Vector2 axis = _input.MoveAxis;
            Vector3 delta = new Vector3(axis.x, 0f, axis.y);
            if (delta.sqrMagnitude > 1f) delta.Normalize(); // 대각 이동 속도 보정

            // MovePosition: transform 직접 이동과 달리 콜라이더를 밀어내며 보간까지 유지된다.
            float multiplier = _state != null ? _state.MoveSpeedMultiplier : 1f;
            _body.MovePosition(_body.position + delta * (_moveSpeed * multiplier * Time.fixedDeltaTime));
        }

        void AimAtPointer()
        {
            if (_camera == null) return;

            // 탑다운이라 커서를 플레이어 높이의 평면에 투영해서 바라볼 지점을 구한다.
            Plane aimPlane = new Plane(Vector3.up, transform.position);
            Ray ray = _camera.ScreenPointToRay(_input.PointerPosition);
            if (!aimPlane.Raycast(ray, out float distance)) return;

            Vector3 look = ray.GetPoint(distance) - transform.position;
            look.y = 0f;
            if (look.sqrMagnitude < 0.0001f) return; // 커서가 발밑이면 직전 방향 유지

            // 확정 스펙: 커서 방향을 8방향으로 스냅한다.
            // 판정이 부채꼴이라 조준이 연속이면 "맞을 각인데 안 맞는다"가 생긴다 — 각을 이산화해 읽기 쉽게.
            if (_facingSnapDegrees > 0f)
            {
                float angle = Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg;
                angle = Mathf.Round(angle / _facingSnapDegrees) * _facingSnapDegrees;
                transform.rotation = Quaternion.Euler(0f, angle, 0f);
                return;
            }

            transform.rotation = Quaternion.LookRotation(look);
        }
    }
}
