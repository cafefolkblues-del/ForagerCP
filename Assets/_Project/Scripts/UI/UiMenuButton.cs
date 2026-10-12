using UnityEngine;
using UnityEngine.UI;

namespace ForagerCP
{
    /// 메뉴의 버튼 한 칸. 누르면 라우터에게 "이 창 열어라"라고 말한다.
    ///
    /// 버튼마다 onClick을 인스펙터에서 끌어 붙이지 않는 이유: 그러면 연결이 씬 파일 안에 숨어
    /// 코드에서 안 보이고, 메뉴가 늘 때마다 손으로 잇다가 하나씩 빠뜨린다.
    [RequireComponent(typeof(Button))]
    public class UiMenuButton : MonoBehaviour
    {
        [SerializeField] UiRouter _router;

        /// 열 창. 비워두면 '닫기' 버튼이 된다.
        [SerializeField] UiWindow _target;

        void Awake()
        {
            if (_router == null) _router = FindFirstObjectByType<UiRouter>();
            GetComponent<Button>().onClick.AddListener(Click);
        }

        void Click()
        {
            if (_router == null) return;

            if (_target == null) _router.CloseAll();
            else _router.OpenPanel(_target);
        }
    }
}
