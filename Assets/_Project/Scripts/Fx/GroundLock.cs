using UnityEngine;

namespace ForagerCP
{
    /// 지면에 붙어 다니는 것의 높이를 고정한다.
    ///
    /// 왜 필요한가: 이동을 MovePosition으로 수평만 지시해도 높이는 물리가 정한다.
    /// 구·캡슐 콜라이더가 상자 모서리에 밀리면 타고 오르고, 겹침 반발이 위로 터지면 솟구친다.
    /// "이런 경우엔 막자"는 조건으로는 예외가 계속 남는다 — 아예 Y 자유도를 주지 않는 쪽이 근본이다.
    ///
    /// 점프가 없다는 확정 스펙 위에서만 성립한다. 날아오르는 연출이나 지형 고저차가 생기면
    /// Release()로 잠깐 풀거나 SetHeight()로 기준을 옮긴다.
    [RequireComponent(typeof(Rigidbody))]
    public class GroundLock : MonoBehaviour
    {
        [SerializeField] bool _lockOnStart = true;

        /// 비워두면(음수) 시작 위치의 높이를 기준으로 삼는다.
        [SerializeField] float _height = float.NegativeInfinity;

        Rigidbody _body;
        RigidbodyConstraints _baseConstraints;
        float _releaseUntil;

        public bool IsLocked => _lockOnStart && Time.time >= _releaseUntil;
        public float Height => _height;

        void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _baseConstraints = _body.constraints;
        }

        /// 기준 높이는 Start에서 잡는다. GridPlacement가 OnEnable에서 칸 좌표로 위치를 맞추기 때문에
        /// Awake 시점 값은 배치 전 좌표일 수 있다(몬스터 HomePosition과 같은 이유).
        void Start()
        {
            if (float.IsNegativeInfinity(_height)) _height = transform.position.y;
            ApplyConstraints();
        }

        void FixedUpdate()
        {
            if (!IsLocked)
            {
                ApplyConstraints();
                return;
            }

            ApplyConstraints();

            // 고정 중에도 텔레포트·리스폰으로 높이가 바뀔 수 있어 한 번 더 맞춘다.
            if (Mathf.Abs(_body.position.y - _height) < 0.001f) return;

            Vector3 corrected = _body.position;
            corrected.y = _height;
            _body.position = corrected;
        }

        void ApplyConstraints()
        {
            RigidbodyConstraints wanted = IsLocked
                ? _baseConstraints | RigidbodyConstraints.FreezePositionY
                : _baseConstraints;

            if (_body.constraints != wanted) _body.constraints = wanted;
        }

        /// 기준 높이를 옮긴다. 지형 높이가 생기면 여기로 주입한다.
        public void SetHeight(float height)
        {
            _height = height;
            if (!IsLocked) return;

            Vector3 corrected = _body.position;
            corrected.y = height;
            _body.position = corrected;
        }

        /// 지정한 시간 동안 고정을 푼다(보스 강하, 띄우는 연출 등).
        public void Release(float seconds)
        {
            _releaseUntil = Time.time + Mathf.Max(0f, seconds);
            ApplyConstraints();
        }

        public void LockNow(bool useCurrentHeight = false)
        {
            _releaseUntil = 0f;
            if (useCurrentHeight) _height = transform.position.y;
            ApplyConstraints();
        }
    }
}
