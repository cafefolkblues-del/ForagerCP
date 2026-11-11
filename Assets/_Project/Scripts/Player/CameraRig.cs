using UnityEngine;

namespace ForagerCP
{
    /// 탑다운 고정각 추적 카메라. 각도는 고정이고 위치만 따라간다.
    /// 시작점~광물지역~상점이 30m 넘게 벌어져 있어 완전 고정 프레임으론 한 화면에 안 들어옴.
    public class CameraRig : MonoBehaviour
    {
        [SerializeField] Transform _target;
        [SerializeField] Vector3 _offset = new Vector3(0f, 18f, -10f);
        [SerializeField] float _followLerp = 8f;

        Vector3 _followed;
        bool _hasFollowed;
        float _shakeAmplitude;
        float _shakeUntil;
        float _shakeDuration;

        /// 잠깐 흔든다(섬이 솟을 때 등). 겹쳐 부르면 더 센 쪽이 이긴다.
        public void Shake(float amplitude, float duration)
        {
            if (Time.time < _shakeUntil && amplitude < CurrentShake) return;

            _shakeAmplitude = amplitude;
            _shakeDuration = Mathf.Max(0.01f, duration);
            _shakeUntil = Time.time + _shakeDuration;
        }

        float CurrentShake => Time.time >= _shakeUntil
            ? 0f
            : _shakeAmplitude * ((_shakeUntil - Time.time) / _shakeDuration);

        void LateUpdate()
        {
            if (_target == null) return;

            // 흔들림은 따라가기 위치와 따로 둔다. 섞으면 다음 프레임 보간이 흔들린 자리에서 출발해 화면이 끌려간다.
            if (!_hasFollowed)
            {
                _followed = transform.position;
                _hasFollowed = true;
            }

            // LateUpdate: 플레이어 이동이 끝난 뒤에 따라붙어야 카메라가 한 프레임 밀리지 않는다.
            Vector3 desired = _target.position + _offset;
            _followed = Vector3.Lerp(_followed, desired, 1f - Mathf.Exp(-_followLerp * Time.deltaTime));

            float shake = CurrentShake;
            Vector3 jitter = shake > 0f
                ? new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(-1f, 1f)) * shake
                : Vector3.zero;

            transform.position = _followed + jitter;
        }
    }
}
