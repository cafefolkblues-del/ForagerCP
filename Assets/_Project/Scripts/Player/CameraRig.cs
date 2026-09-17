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

        void LateUpdate()
        {
            if (_target == null) return;

            // LateUpdate: 플레이어 이동이 끝난 뒤에 따라붙어야 카메라가 한 프레임 밀리지 않는다.
            Vector3 desired = _target.position + _offset;
            transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-_followLerp * Time.deltaTime));
        }
    }
}
