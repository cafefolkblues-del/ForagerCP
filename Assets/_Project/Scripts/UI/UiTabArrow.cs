using UnityEngine;
using UnityEngine.UI;

namespace ForagerCP
{
    /// 탭 화면 양옆의 화살표. 누르면 라우터에게 "옆 탭으로"라고 말한다.
    ///
    /// onClick을 인스펙터에서 끌어 붙이지 않는 이유: 그러면 연결이 씬 파일 안에 숨어
    /// 코드에서 안 보인다. 방향만 인스펙터에 둔다.
    [RequireComponent(typeof(Button))]
    public class UiTabArrow : MonoBehaviour
    {
        [SerializeField] UiRouter _router;

        /// -1 = 이전 탭(왼쪽), +1 = 다음 탭(오른쪽).
        [SerializeField] int _direction = 1;

        void Awake()
        {
            if (_router == null) _router = FindFirstObjectByType<UiRouter>();
            GetComponent<Button>().onClick.AddListener(Click);
        }

        void Click()
        {
            if (_router != null) _router.StepTab(_direction);
        }
    }
}
