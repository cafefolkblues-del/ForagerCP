using UnityEngine;

namespace ForagerCP
{
    /// 배치물을 담는 그룹 표시. 배치 도구는 이 컴포넌트가 붙은 오브젝트만 "넣을 그룹"으로 인정한다.
    /// 디자이너가 실수로 플레이어나 HUD 밑에 구조물을 넣는 사고를 막는 게 목적.
    [DisallowMultipleComponent]
    public class MapFolder : MonoBehaviour
    {
        [SerializeField] string _label = "구조물";

        /// 팔레트 항목의 Category와 이 값이 같으면 자동으로 이 그룹에 들어간다.
        [SerializeField] string _category = "Structures";

        public string Label => _label;
        public string Category => _category;

        public void Setup(string label, string category)
        {
            _label = label;
            _category = category;
        }
    }
}
